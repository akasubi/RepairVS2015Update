module AppSettings

open System
open System.IO

open Newtonsoft.Json


let private vendorName = "wezeku"
let private appName    = "RepairVS2015Update"


type AppSettingsData() =
    member val CorrectCollectionsImmutableVersion = "" with get, set


let private settingsFolder = 
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        vendorName,
        appName)

let private settingsPath =
    Path.Combine(settingsFolder, appName + ".json")


let read() =
    if File.Exists(settingsPath) then
        let json = File.ReadAllText(settingsPath)
        JsonConvert.DeserializeObject<AppSettingsData>(json)
    else
        AppSettingsData(CorrectCollectionsImmutableVersion = "1.1.37.0")


let write (data: AppSettingsData) =
    let json = JsonConvert.SerializeObject(data)
    Directory.CreateDirectory(settingsFolder) |> ignore
    File.WriteAllText(settingsPath, json)
