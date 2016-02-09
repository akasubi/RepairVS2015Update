module RepairVS2015UpdateLib

open System
open System.IO
open System.Linq
open System.Drawing
open System.Reflection
open System.Text.RegularExpressions
open System.Windows
open System.Windows.Controls
open System.Windows.Interop
open System.Windows.Media.Imaging
open System.Windows.Shapes
open System.Xml.Linq
open System.Windows.Data


let appTitle = "VS2015 update config repair"

let asmBindingNs = "urn:schemas-microsoft-com:asm.v1"


let vs2015LocalDataDir = 
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 @"Microsoft\VisualStudio\14.0")

let vs2015InstallDir =
    let registryKeyString = sprintf @"SOFTWARE%s\Microsoft\VisualStudio\14.0"
                                    (if not Environment.Is64BitProcess then @"\Wow6432Node" else "")
    use localMachineKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKeyString)
    localMachineKey.GetValue("InstallDir") :?> string


let deleteComponentModelCache () =
    let cacheDir = Path.Combine(vs2015LocalDataDir, "ComponentModelCache")
    if Directory.Exists(cacheDir) then
        try
            Directory.Delete(cacheDir, true)
            (true, "ComponentModelCache deleted.")
        with exn -> (false, exn.Message)
    else (true, "ComponentModelCache directory doesn't exist.")


let getProbeDirectories (xdoc: XDocument) =
    let pathStr =
        xdoc.Root
            .Element(XName.Get("runtime"))
            .Element(XName.Get("assemblyBinding", asmBindingNs))
            .Element(XName.Get("probing", asmBindingNs))
            .Attribute(XName.Get("privatePath"))
            .Value
    pathStr.Split(';')
    |> Array.toList
    |> List.map (fun s -> Path.Combine(vs2015InstallDir, s))


let getDependentAssemblies (xdoc: XDocument) =
    let nameAttrs =
        xdoc.Root
            .Element(XName.Get("runtime"))
            .Element(XName.Get("assemblyBinding", asmBindingNs))
            .Elements(XName.Get("dependentAssembly", asmBindingNs))
            .Elements(XName.Get("assemblyIdentity", asmBindingNs))
            .Attributes(XName.Get("name"))
    nameAttrs |> Seq.map (fun a -> a.Value)


let getBindingRedirectNewVer (xdoc: XDocument) assemblyIdentity =
    try
        xdoc.Root
            .Element(XName.Get("runtime"))
            .Element(XName.Get("assemblyBinding", asmBindingNs))
            .Elements(XName.Get("dependentAssembly", asmBindingNs))
            .Single(fun e -> e.Element(XName.Get("assemblyIdentity", asmBindingNs))
                                            .Attribute(XName.Get("name")).Value = assemblyIdentity)
            .Element(XName.Get("bindingRedirect", asmBindingNs))
            .Attribute(XName.Get("newVersion"))
        |> Some
    with _ -> None


type AssemblyInfo =
    { Version: string
      PKToken: string }

let getAssemblyInfos (settings: AppSettings.AppSettingsData) assemblyDirectories =
    let loadAsm name =
        assemblyDirectories |> List.tryPick (fun dir ->
            let fName = Path.Combine(dir, name + ".dll")
            if File.Exists(fName) 
            then Some (Assembly.ReflectionOnlyLoadFrom(fName))
            else None)

    let asmInfos =
        [for r in settings.Redirections ->
            match loadAsm r.AssemblyName with
            | Some asm ->
                let matches = Regex.Match(asm.FullName, ".*Version=([^,]*),.*PublicKeyToken=(.*)")
                let ver = matches.Groups.[1].Value
                let pkToken = matches.Groups.[2].Value
                (r, Some { Version = ver; PKToken = pkToken })
            | None -> (r, None)
        ]
    asmInfos


type BindingRedirectAction =
    | Add
    | Modify
    | Ignore of string  // string = reason for ignore.

type BindingRedirectChange =
    { Redirection: AppSettings.Redirection
      AssemblyInfo: AssemblyInfo option
      NewVersionAttr: XAttribute option
      Action: BindingRedirectAction
    }

let determineRedirectChanges (cfgXdoc: XDocument) settings =
    let asmDirs = getProbeDirectories cfgXdoc
    let asmInfos = getAssemblyInfos settings asmDirs
    [for (redir, asmInfoOpt) in asmInfos ->
        match asmInfoOpt with
        | None ->
            { Redirection = redir
              AssemblyInfo = None
              NewVersionAttr = None
              Action = Ignore "Assembly file not found" }
        | Some asmInfo ->
            let presentNewVerOpt = getBindingRedirectNewVer cfgXdoc redir.AssemblyName
            match presentNewVerOpt with
            | Some presentNewVer ->
                let action =
                    if presentNewVer.Value = asmInfo.Version
                    then Ignore "newVersion already correct"
                    else Modify
                { Redirection = redir
                  AssemblyInfo = Some asmInfo
                  NewVersionAttr = presentNewVerOpt
                  Action = action }
            | None ->
                let action =
                    if isNull redir.OldVersion 
                    then Ignore "Missing oldVersion setting"
                    else Add
                { Redirection = redir
                  AssemblyInfo = Some asmInfo
                  NewVersionAttr = None
                  Action = action }
    ]


let applyBindingRedirectChanges cfgFullPath (cfgXdoc: XDocument) (changes: BindingRedirectChange list) =
    let rec bakFName n =
        if not (File.Exists(cfgFullPath + ".bak")) then cfgFullPath + ".bak"
        else
            let tryName = sprintf "%s%s%d" cfgFullPath ".bak" n
            if File.Exists(tryName)
            then bakFName (n+1)
            else tryName

    for change in changes do
        match change.Action with
        | Add -> ()
        | Modify ->
            change.NewVersionAttr.Value.Value <- change.AssemblyInfo.Value.Version            
        | Ignore _ -> ()

    if changes |> List.exists (fun c -> match c.Action with Add | Modify -> true | Ignore _ -> false) then
        File.Copy(cfgFullPath, bakFName 1, true)
        cfgXdoc.Save(cfgFullPath)
        (true, "Binding redirections where updated.")
    else (true, "No binding redirection update needed.")


// Display data for the grid view
type DisplayChange =
    { AssemblyName: string
      OldVer: string
      NewVer: string
      ShouldBe: string 
      Action: string }


let assemblyDisplayList cfgXdoc bindingRedirectChanges =
    [for i in bindingRedirectChanges ->
        { AssemblyName = i.Redirection.AssemblyName
          OldVer = i.Redirection.OldVersion
          NewVer = 
            match i.NewVersionAttr with
            | Some a -> a.Value
            | None -> ""
          ShouldBe =
            match i.AssemblyInfo with
            | Some info -> info.Version
            | None -> ""
          Action =
            match i.Action with
            | Add -> "Add bindingRedirect"
            | Modify -> "Modify bindingRedirect"
            | Ignore s -> "Ignore: " + s
        } 
    ]


let applyFixes cfgFullPath cfgXdoc bindingRedirectChanges (window: Window) =
    let (result1, msg1) = deleteComponentModelCache ()
    let (result2, msg2) = applyBindingRedirectChanges cfgFullPath cfgXdoc bindingRedirectChanges
    let image = if result1 && result2 then MessageBoxImage.Information else MessageBoxImage.Error
    MessageBox.Show(msg1 + "\n\n" + msg2, appTitle, MessageBoxButton.OK, image) |> ignore
    if result1 && result2 then window.Close()


let editSettingsFile _ =
    System.Diagnostics.Process.Start(AppSettings.settingsPath)
    |> ignore


let openStackOverflowPage _ =
    System.Diagnostics.Process.Start("http://stackoverflow.com/questions/31547947/packages-not-loading-after-installing-visual-studio-2015-rtm")
    |> ignore


let assemblyListView margin =
    let gv = GridView()
    GridViewColumn(Header="Assembly", DisplayMemberBinding = new Binding("AssemblyName")) |> gv.Columns.Add 
    GridViewColumn(Header="oldVersion", DisplayMemberBinding = new Binding("OldVer")) |> gv.Columns.Add
    GridViewColumn(Header="newVersion", DisplayMemberBinding = new Binding("NewVer")) |> gv.Columns.Add
    GridViewColumn(Header="newVersion should be", DisplayMemberBinding = new Binding("ShouldBe")) |> gv.Columns.Add
    GridViewColumn(Header="Action", DisplayMemberBinding = new Binding("Action")) |> gv.Columns.Add
    ListView(Margin = margin, View = gv)


let createUI (icon: Icon) cfgFullPath cfgXdoc = 
    let iconAsImageSource = 
        Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())

    let controlStack = StackPanel(Orientation = Orientation.Vertical, Margin = Thickness(10.))
    let spacing = Thickness(0., 0., 0., 10.)

    let addButton f content =
        let button = Button(Content = content, 
                            Margin = spacing, 
                            Padding = Thickness(12., 6., 12., 6.))
        button.Click.Add f
        controlStack.Children.Add(button) |> ignore

    let window = Window(Content = controlStack,
                        Icon = iconAsImageSource,
                        SizeToContent = SizeToContent.WidthAndHeight,
                        Title = appTitle)

    let listView = assemblyListView spacing
    controlStack.Children.Add(listView) |> ignore

    let mutable bindingRedirectChanges = []
    let loadSettings _ =
        let asmNames = getDependentAssemblies cfgXdoc
        let settings: AppSettings.AppSettingsData =
            { Redirections =
                [for i in asmNames -> { AssemblyName = i; OldVersion = null } ]
            }
//        let settings = AppSettings.read()
        bindingRedirectChanges <- determineRedirectChanges cfgXdoc settings
        let list = assemblyDisplayList cfgXdoc bindingRedirectChanges
        listView.ItemsSource <- list
        listView.Items.Refresh()

    loadSettings()

    addButton editSettingsFile "_Edit settings file"
    addButton loadSettings "_Reload settings file"
    controlStack.Children.Add(Rectangle(Fill = Media.Brushes.Black, Height = 2., Margin = spacing)) |> ignore
    addButton (fun _ -> applyFixes cfgFullPath cfgXdoc bindingRedirectChanges window) 
              "_Apply binding redirection fixes and delete ComponentModelCache"
    controlStack.Children.Add(Rectangle(Fill = Media.Brushes.Black, Height = 2., Margin = spacing)) |> ignore
    addButton openStackOverflowPage "Open _Stackoverflow question about this problem"

    window


let main(icon: Icon) = 
    let cfgFName = "devenv.exe.config"
    let cfgFullPath = Path.Combine(vs2015LocalDataDir, cfgFName)
    let cfgXdoc = XDocument.Load(cfgFullPath)

    let app = Application()
    let window = createUI icon cfgFullPath cfgXdoc

    app.Run(window) |> ignore
