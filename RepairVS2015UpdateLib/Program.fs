module RepairVS2015UpdateLib

open System
open System.IO
open System.Linq
open System.Windows
open System.Windows.Controls
open System.Windows.Shapes
open System.Xml.Linq


let msgBoxTitle = "VS2015 update config repair"

let vs2015LocalDataDir = 
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 @"Microsoft\VisualStudio\14.0")


let deleteComponentModelCache () =
    let cacheDir = Path.Combine(vs2015LocalDataDir, "ComponentModelCache")
    if Directory.Exists(cacheDir) then
        try
            Directory.Delete(cacheDir, true)
            (true, "ComponentModelCache deleted.")
        with exn -> (false, exn.Message)
    else (true, "ComponentModelCache directory doesn't exist.")


let getImmutableCollectionNewVer (xdoc: XDocument) =
    let asmBindingNs = "urn:schemas-microsoft-com:asm.v1"
    try
        xdoc.Root
            .Element(XName.Get("runtime"))
            .Element(XName.Get("assemblyBinding", asmBindingNs))
            .Elements(XName.Get("dependentAssembly", asmBindingNs))
            .Single(fun e -> e.Element(XName.Get("assemblyIdentity", asmBindingNs))
                                        .Attribute(XName.Get("name")).Value = "System.Collections.Immutable")
            .Element(XName.Get("bindingRedirect", asmBindingNs))
            .Attribute(XName.Get("newVersion"))
        |> Some
    with _ -> None


let fixDevEnvConfig cfgFullPath cfgXdoc versionNo =
    match getImmutableCollectionNewVer cfgXdoc with
    | Some newVer ->
        if newVer.Value <> versionNo then 
            File.Copy(cfgFullPath, cfgFullPath + ".bak", true)
            newVer.Value <- versionNo
            cfgXdoc.Save(cfgFullPath)
            (true, "Binding redirection was updated.")
        else
            (true, "No change to binding redirection needed.")
    | None -> (false, "System.Collections.Immutable binding redirection not found.")


let callAndReport f (window: Window) _ =
    let (result, msg) = f ()
    let image = if result then MessageBoxImage.Information else MessageBoxImage.Error
    MessageBox.Show(msg, msgBoxTitle, MessageBoxButton.OK, image) |> ignore
    if result then window.Close()

let deleteCacheFixConfigAndReport cfgFullPath cfgXdoc versionNo (window: Window) =
    let (result1, msg1) = deleteComponentModelCache ()
    let (result2, msg2) = fixDevEnvConfig cfgFullPath cfgXdoc versionNo
    let image = if result1 && result2 then MessageBoxImage.Information else MessageBoxImage.Error
    MessageBox.Show(msg1 + "\n\n" + msg2, msgBoxTitle, MessageBoxButton.OK, image) |> ignore
    if result1 && result2 then window.Close()

let openStackOverflowPage _ =
    System.Diagnostics.Process.Start("http://stackoverflow.com/questions/31547947/packages-not-loading-after-installing-visual-studio-2015-rtm")    
    |> ignore


type UI =
    { Window: Window
      VersionInput: TextBox }

let createUI cfgFullPath cfgXdoc = 
    let controlStack = StackPanel(Orientation = Orientation.Vertical, Margin = Thickness(10.))
    let spacing = Thickness(0., 0., 0., 10.)

    let addButton f content =
        let button = Button(Content = content, 
                            Margin = spacing, 
                            Padding = Thickness(12., 6., 12., 6.))
        button.Click.Add f
        controlStack.Children.Add(button) |> ignore

    let window = Window(Content = controlStack, SizeToContent = SizeToContent.WidthAndHeight)

    let addVersionOutput() =
        let presentNewVer =
            match getImmutableCollectionNewVer cfgXdoc with
            | Some ver -> ver.Value
            | None -> "Value not set."
        controlStack.Children.Add(Label(Content = "Collections.Immutable newVersion is: " + presentNewVer,
                                        Margin = spacing)) |> ignore

    let addVersionInput() =
        let sp = StackPanel(Orientation = Orientation.Horizontal, Margin = spacing)
        controlStack.Children.Add(sp) |> ignore
        sp.Children.Add(Label(Content = "Collections.Immutable newVersion should be: ")) |> ignore
        let textBox = TextBox(VerticalContentAlignment = VerticalAlignment.Center, Width = 100.)
        sp.Children.Add(textBox) |> ignore
        textBox

    addVersionOutput()
    let versionInput = addVersionInput()
    addButton (callAndReport deleteComponentModelCache window) "_Delete ComponentModelCache"
    addButton (callAndReport (fun _ -> fixDevEnvConfig cfgFullPath cfgXdoc versionInput.Text) window) 
                             "_Fix Collections.Immutable\nnewVersion binding redirect"
    addButton (fun _ -> deleteCacheFixConfigAndReport cfgFullPath cfgXdoc versionInput.Text window) 
              "Do _both of the above"
    controlStack.Children.Add(Rectangle(Fill = Media.Brushes.Black, Height = 2., Margin = spacing)) |> ignore
    addButton openStackOverflowPage "Open _Stackoverflow question about this problem"

    { Window = window
      VersionInput = versionInput }


let main() = 
    let cfgFName = "devenv.exe.config"
    let cfgFullPath = Path.Combine(vs2015LocalDataDir, cfgFName)
    let cfgXdoc = XDocument.Load(cfgFullPath)
    let settings = AppSettings.read()

    let app = Application()
    let ui = createUI cfgFullPath cfgXdoc
    ui.VersionInput.Text <- settings.CorrectCollectionsImmutableVersion

    app.Run(ui.Window) |> ignore

    settings.CorrectCollectionsImmutableVersion <- ui.VersionInput.Text
    AppSettings.write settings
