# Discord Notification

## 2.0.0.10
- Bugfix: Removed wrong notification

## 2.0.0.9
- Feature: Added dynamic patterns to the message input field. ("$$TARGET$$", "$$DATE$$", "$$DATEMINUS12$$", "$$DATETIME$$", "$$TIME$$", "$$TARGET$$", "$$FILTER$$", "$$RMSRA$$", "$$RMSDEC$$", "$$RMSTOTAL$$")
- Feature: Added the option "Send Embeds" to enable or disable embedded fields in the Discord message.

## 2.0.0.8
- Feature: Added additional logging for better traceability and debugging.
- Bugfix: Implemented today’s date as a fallback when the thread name is empty and resolved several issues related to message sending.
 
## 2.0.0.7
- Feature: Added support for using patterns in thread names (e.g., $$FILTER$$).
- Feature: Added the option “Select Filters to Send Images For” to triggers, allowing multiple filters to be selected for live-stacked image sending.
- Feature: Introduced a "Custom Filters for the Dropdown List" setting to extend the available filter list.
- Feature: Implemented a queue to optimize and manage the image sending process.
- Bugfix: Improved the image sending workflow for better consistency - the system now properly waits for file changes before sending.

## 2.0.0.6
- Feature: added buttons to the plugin option page for testing the Discord Webhook & Discord Bot
- Feature: added patterns to define the thread name ("$$TARGET$$", "$$DATE$$", "$$DATEMINUS12$$", "$$DATETIME$$", "$$TIME$$")
- Bugfix: fixed error message "Cannot start an already running client" & fix for creating threads when no WebhookUrl is provided

## 2.0.0.5
- Feature: added trigger to create a thread named after the target and send a message to Discord after a specified number of exposures

## 2.0.0.4
- Bugfix: fixed trigger when using OSC cameras

## 2.0.0.3
- Bugfix: fixed entity too large exception from discord
- Feature: added scaling property for images

## 2.0.0.2
- Bugfix: fixed counting of exposures in when using take many exposures instruction

## 2.0.0.1
- Feature: added checkbox to trigger for using live stacked images

## 1.0.0.1
- Feature: added trigger for sending messages to discord after x exposures taken