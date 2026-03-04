using Accord.Imaging.Filters;
using Discord;
using Google.Protobuf.WellKnownTypes;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("e172f5a2-40b5-4180-bd38-395c307ea172")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("2.0.0.10")]
[assembly: AssemblyFileVersion("2.0.0.10")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Discord Notification")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("This N.I.N.A plugin sends Discord notifications and can share images (e.g., live stacked images) from a specific folder, all integrated into the sequencer.")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("Daniel Ludwig")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Discord Notification")]
[assembly: AssemblyCopyright("Copyright © 2025 Daniel Ludwig")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.1018")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Discord")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/CHANGELOG.md")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/assets/notification_1.png?raw=true")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/assets/notification_settings.png?raw=true")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/assets/livestacking_settins.png?raw=true")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"This plugin for N.I.N.A allows users to send notifications directly to Discord.
It can be integrated as a trigger into the sequencer in N.I.N.A, providing seamless communication during imaging sessions.
Additionally, users can specify an image path - such as a folder where a live stacked image is stored - which will also be sent to Discord.
This makes it easy to share updates and images in real time.
The plugin also includes a trigger that can automatically create a new Discord thread and post a message after exposures, helping to keep session-related messages organized.

### Beta vs Release Channel
- Discord Notification's current versions require N.I.N.A 3.2, which is currently in the Beta release channel. Basic functionality from verison 1.0.0.1 is available in the N.I.N.A. 3.1 Release channel.

### Information
- If the thread name is left empty, the current date will automatically be used as a fallback value.

## LiveStack
- The LiveStack image feature needs the 'LiveStack' plugin to be installed and configured as shown in the screenshots below.
### Important!
- The plugin must have live stacking started before images can be sent.
- You should also configure a minimum delay of 60 seconds after the exposures are finished, before stopping the live stacking process.
- The “LiveStacked Image Path” must be set to the folder where the stacked files created by the LiveStack plugin are stored.
- To ensure reliable sending of live stacked images, it is recommended to use at least 4 iterations of the loop condition.

## General Options
- **Discord Webhook URL:**
The webhook URL of your Discord webhook integration.

- **Image Scale Factor:**
The factor used to scale the image down.

- **LiveStacked Image Directory:**
The directory where the live stacked images are stored.

- **Send Embeds:**
Enables or disables embedded fields in the Discord message.

- **Custom Filters for the Dropdown List:**
Filters to extend the dropdown list (comma-separated).

## Thread Options
- **Discord Bot Token:**
The authentication token of your Discord bot. Required to create threads and send messages.

- **Discord Channel ID:**
The ID of the channel where the thread will be created. Must be a text channel your bot has access to.

- **Thread name template:**
The thread name is defined using a pattern that can include the following placeholders: $$TARGET$$, $$DATE$$, $$DATEMINUS12$$, $$DATETIME$$, $$TIME$$, $$FILTER$$.

- **Thread Archive Duration:**
Defines how long the created thread stays active before being auto-archived by Discord.

## Options for the Trigger
- **Message:**
The input field for the message that should be sent. Can be left empty.
This field supports dynamic patterns that will be automatically replaced with live data when the message is sent:
    - **$$DATE$$**, **$$DATEMINUS12$$**, **$$DATETIME$$**, **$$TIME$$**, **$$TARGET$$**, **$$FILTER$$**, **$$RMSRA$$**, **$$RMSDEC$$**, **$$RMSTOTAL$$**

- **After Exposures:**
Specifies after how many exposures the message should be sent.

- **Send Image:**
When checked, the last auto-stretched image is sent with image data.

- **Use LiveStack Image:**
When checked, the last live-stacked images are sent based on the filters selected in **Select Filters to Send Images For**.

- **Select Filters to Send Images For:**
Select multiple filters to send the live-stacked images.
    - **RGB**: Sends the live-stacked image with the **RGB** filter When using a monochrome cameras, you need to configure a color combination in the LiveStack plugin.
    - **$$FILTER$$**: Sends the live-stacked image based on the current filter.
")]


// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]