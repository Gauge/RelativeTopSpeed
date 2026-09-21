# Updating the existing Workshop mod

Workshop item: [Relative Top Speed, 1359618037](https://steamcommunity.com/sharedfiles/filedetails/?id=1359618037).

`RelativeTopSpeed/modinfo.sbmi` links the upload folder to that existing item. Its owner ID, `76561198079985653`, was verified through Steam's public GetPublishedFileDetails API and matches the owner's other local mod metadata. Log into that Steam account when publishing. Editing this file does not grant ownership of an item.

## Prepare the local mod

Copy the **inner** `RelativeTopSpeed` folder into the game's local `Mods` directory. Do not copy the repository root, test projects or build output. The layout must be:

```text
SpaceEngineers/Mods/RelativeTopSpeed/
  modinfo.sbmi
  Data/
    Scripts/
      RTS/
        ...all current source files and SENetworkAPI...
```

Windows local mods are under `%APPDATA%\SpaceEngineers\Mods`.

On Linux using Proton, look inside the game's Steam library at `steamapps/compatdata/244850/pfx/drive_c/users/steamuser/AppData/Roaming/SpaceEngineers/Mods`. A custom Wine/Proton prefix may use a different location.

If an old local copy already exists, back it up outside `Mods` and replace its `Data` folder with the current one. Overlaying files can leave obsolete C# files behind and cause duplicate definitions. Keep the supplied `modinfo.sbmi` alongside `Data`. Preserve existing thumbnail artwork if available.

## Publish the update

1. Test the local mod in a disposable world, with the subscribed copy disabled for that test.
2. Open **Load Game**, select a world, then **Edit Settings → Mods**.
3. Select the **local** RelativeTopSpeed entry (folder icon).
4. Click **Publish** and select Steam if prompted.
5. Confirm that the game asks to overwrite/update the existing item. If it offers to create a new item, cancel and check that `modinfo.sbmi` is in the selected local folder.
6. After uploading, verify that the existing item at ID `1359618037` has the new update time.

The legacy top-level `<WorkshopId>0</WorkshopId>` is intentional: the actual Steam item ID is in the modern `WorkshopIds` list. Do not remove `modinfo.sbmi` when preparing later updates.

Publishing is a separate in-game action; adding this metadata does not upload anything.

Reference: [Digi's modding guide: publishing and updating without modinfo.sbmi](https://github.com/THDigi/SE-ModScript-Examples/wiki/Quick-Intro-to-Space-Engineers-Modding#updating-without-modinfosbmi).
