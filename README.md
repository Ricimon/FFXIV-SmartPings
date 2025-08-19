# SmartPings

Plugin for [XIVLauncher/Dalamud](https://goatcorp.github.io/)

This plugin adds networked ground and UI pings to FFXIV as a Dalamud plugin.

## Features

### Ground pings
![](images/ground_pings.gif)

### Networked pings
![](images/networked_pings.gif)

### UI Pings
![](images/ui_pings.gif)

## Support discord

[![Discord Banner](https://discord.com/api/guilds/669688899248979968/widget.png?style=banner2)](https://discord.gg/rSucAJ6A7u)

## Installation
- Enter `/xlsettings` in the chat window and go to the **Experimental** tab in the opened window.
- Scroll down to the **Custom Plugin Repositories** section.
- Paste in the following `repo.json` link into the first open text field
```
https://raw.githubusercontent.com/Ricimon/FFXIV-ProximityVoiceChat/refs/heads/master/repo.json
```
*Both ProximityVoiceChat and SmartPings are accessible from the ProximityVoiceChat repo json*
- Click the **+** button to the right of the text field and make sure the **Enabled checkmark** is checked.
- Click on the **Save Button** on the bottom-right of the window.

This adds plugins from this repo as installable plugins in the available plugins list. To then install the plugin itself,

- Enter `/xlplugins` in the chat window and go to the **All Plugins** tab in the opened window.
- Search for the **SmartPings** plugin and click **install**.

## Usage

Default ping keybinds are `G` then left click to execute a ping, or hold `Control` then left click to execute a quick ping.

Keybinds are adjustable in the plugin config settings, which can be opened by typing either `/smartpings` or `/sp` into the chat.

To send and receive pings from other players, join either a public or private room.<br />
A public room will automatically match you with players in your map, while private rooms are password protected and keeps your room between map changes.

UI pings are sent through echo chat by default, but can be configured to send through in-game chat, such as party chat. However, use this feature with caution, as in-game chat logs are recorded by Square Enix.

Currently supported UI pings:
- Own statuses
- Party list statuses
- Party list HP/MP
- Target statuses
- Target HP

## Contributing
Please use the support discord for idea and code contribution discussion.

This plugin's servers are self-hosted and the development team is one person, so any donations are well appreciated.

[![ko-fi](https://www.ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/ricimon)
