# Discord Notification

This plugin for N.I.N.A allows users to send notifications directly to Discord.
It can be integrated as a trigger into the sequencer in N.I.N.A, providing seamless communication during imaging sessions.
Additionally, users can specify an image path - such as a folder where a live stacked image is stored - which will also be sent to Discord.
This makes it easy to share updates and images in real time.
The plugin also includes a trigger that can automatically create a new Discord thread and post a message after exposures, helping to keep session-related messages organized.

## Options
- **LiveStack:**
The LiveStack image feature needs the 'LiveStack' plugin to be installed and configured as shown in the screenshots below.

## Thread Options
- **Discord Bot Token:**
The authentication token of your Discord bot. Required to create threads and send messages.

- **Discord Channel ID:**
The ID of the channel where the thread will be created. Must be a text channel your bot has access to.

- **Thread name template:**
The thread name is defined using a pattern that can include the following placeholders: $$TARGET$$, $$DATE$$, $$DATEMINUS12$$, $$DATETIME$$, $$TIME$$.

- **Thread Archive Duration:**
Defines how long the created thread stays active before being auto-archived by Discord.

![notification_settings](https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/assets/livestacking_settins.png)
![livestacking_settins](https://github.com/CodeByLudwig/N.I.N.A-Discord-Notification/blob/develop/assets/notification_settings.png)

