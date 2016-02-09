module AppSettings

open System
open System.IO

open Newtonsoft.Json


let private vendorName = "wezeku"
let private appName    = "RepairVS2015Update"


type Redirection =
    { AssemblyName: string
      OldVersion: string
    }


type AppSettingsData =
    { Redirections: Redirection list
    }


let private settingsFolder = 
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        vendorName,
        appName)

let settingsPath =
    Path.Combine(settingsFolder, appName + ".json")


let private write (data: AppSettingsData) =
    let json = JsonConvert.SerializeObject(data, Formatting.Indented)
    Directory.CreateDirectory(settingsFolder) |> ignore
    File.WriteAllText(settingsPath, json)


let read() =
    if File.Exists(settingsPath) then
        let json = File.ReadAllText(settingsPath)
        JsonConvert.DeserializeObject<AppSettingsData>(json)
    else
        let data =
            { Redirections =
                [ { AssemblyName = "System.Collections.Immutable"; OldVersion = null }
                  { AssemblyName = "Microsoft.VisualStudio.ProjectSystem.V14Only"; OldVersion = "14.0.0.0" }
                ]
            }
        write data
        data
