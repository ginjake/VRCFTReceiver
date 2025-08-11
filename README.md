# VRCFTReceiver

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod, that let's you use [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) Program for Eye and Face Tracking inside [Resonite](https://resonite.com/).

> [!WARNING]
> This is not a Plug and Play solution, it requires setup in-game.

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [VRCFTReceiver.dll](https://github.com/ginjake/VRCFTReceiver/releases/latest/download/VRCFTReceiver.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
3. vrc-oscquery-lib.dll and MeaMod.DNS.dll to rml_libs folder, you find it where resonite is installed.
4. Launch VRCFaceTracking
5. Start the game. If you want to verify that the mod is working you can check your Resonite logs.

> [!NOTE]
> As of v1.0.3, `vrc_parameters.json` template with all the parameters now gets created at `C:\Users\{USER}\AppData\LocalLow\VRChat\VRChat\OSC\vrcft\Avatars` on initial install, so you don't need to copy it over manually anymore. You can edit this file if you wish to change the parameters.

## Requirements

- [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) 5.2.3
- Works on Resonite Splittening
- Requires Splittening mod environment

## How it works

Basically the way VRCFT works is that it waits for a OSC message that says which json file to load at `C:\Users\{USER}\AppData\LocalLow\VRChat\VRChat\OSC\{USER_UUID}\Avatars` which your VRChat avatar basically generates (If you use VRCFT Template). In this Json file, it includes all the parameters your avatar requires.

Since we aren't using VRCFT for _VRChat_, we need to get creative and create our own JSON file with parameters we need. You can find example of my own parameters file in `/static/vrc_parameters.json`. If you need access to any other parameters, you need to add it by basically copy pasting the same template used for each paramter. For example:

```json
{
  "name": "FT/v2/EyeLeftX", // Paramter name
  "input": {
    "address": "/avatar/parameters/FT/v2/EyeLeftX", // Paramter name address
    "type": "Float"
  },
  "output": {
    "address": "/avatar/parameters/FT/v2/EyeLeftX", // Paramter name address
    "type": "Float"
  }
}
```

> You can find all of the paramters here at [VRCFT Docs](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters) (FYI: They aren't exactly 1:1 to the one used for the JSON, might need to look into how some VRC avatars do it or ask in their [Discord](https://discord.com/invite/vrcft), You can find me there as well if you need help)


## Credits

- [Based on dfgHiatus's VRCFaceTracking Wrapper Code](https://github.com/dfgHiatus/VRCFT-Module-Wrapper/blob/master/VRCFTModuleWrapper/OSC/VRCFTOSC.cs)
- [Bunch of help from art0007i](https://github.com/art0007i)
- [Help from knackrack615](https://github.com/knackrack615)
- [Splittening support by ginjake](https://x.com/sirojake)

