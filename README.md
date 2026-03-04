# Discord Notification

This plugin for N.I.N.A allows users to send notifications directly to Discord.
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
The thread name is defined using a pattern that can include the following placeholders: "$$TARGET$$", "$$DATE$$", "$$DATEMINUS12$$", "$$DATETIME$$", "$$TIME$$", "$$FILTER$$".

- **Thread Archive Duration:**
Defines how long the created thread stays active before being auto-archived by Discord.

## Options for the Trigger
- **Message:**
The input field for the message that should be sent. Can be left empty.
- This field supports dynamic patterns that will be automatically replaced with live data when the message is sent:
    - **"$$DATE$$"**, **"$$DATEMINUS12$$"**, **"$$DATETIME$$"**, **"$$TIME$$"**, **"$$TARGET$$"**, **"$$FILTER$$"**, **"$$RMSRA$$"**, **"$$RMSDEC$$"**, **"$$RMSTOTAL$$"**


- **After Exposures:**
Specifies after how many exposures the message should be sent.

- **Send Image:**
When checked, the last auto-stretched image is sent with image data.

- **Use LiveStack Image:**
When checked, the last live-stacked images are sent based on the filters selected in **Select Filters to Send Images For**.

- **Select Filters to Send Images For:**
Select multiple filters to send the live-stacked images.
    - **RGB**: Sends the live-stacked image with the **RGB** filter When using a monochrome cameras, you need to configure a color combination in the LiveStack plugin.
    - **"$$FILTER$$"**: Sends the live-stacked image based on the current filter.

![notification_settings](https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/assets/livestacking_settins.png)
![livestacking_settins](https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/assets/notification_settings.png)

