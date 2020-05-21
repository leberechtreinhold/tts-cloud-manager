# TTS Cloud Manager

Manage the cloud files of Tabletop Simulator

## Features

Allows you to connect to the steam cloud, getting all the files on TTS on a neat tree, allowing you to mass upload to a given folder or to delete files.

![Example image](/example_image.png)

## How to use

- Read the "Risks" section.
- Download from the Releases tab on GitHub and extract the zip somewhere you can write (desktop, for example).
- Open Steam with the same user that will execute the tool.
- Execute the tool.
- First, click "Get Data", to update a tree with all your files in their respective folders.
- If you want to upload one or more files, select the folder where you want to leave the files, then click upload and select the files.
- If you want to delete a file, simply select it and click delete.

Note that before and after modifying the tree (uploading/deleting), the tool will leave a backup of the cloud files that TTS uses for management. You should save them as backup in case anything bad occurs.

## Risks

This is a third party tool, and this of course means that there are some risks involved, since the format of TTS is not public, although is not obfuscated.

For this reason, consider that this application will in no way guarantee that it will not wreck your game. It does make a backup of the CloudInfo so recovery is possible, but still, there are risks involved. Furthermore, ensure that the version supported is the same that it says here! If TTS updates and changes the format, this could wreck your game.

The current supported TTS version is v12.4.

## For devs

The code is MIT, and PRs are welcome. For a dev environment, clone or download this repository, and then download Steamworks.NET and reference it. You will also need to ensure that the steam_api.dll is deployed next to the binaries.

The future features should be multideletion, moving files between folders, and creating new folders.