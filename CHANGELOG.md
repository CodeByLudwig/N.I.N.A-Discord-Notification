# Discord Notification

## 2.0.0.7
- Feature: added patterns to define the thread name "$$FILTER$$")

## 2.0.0.6
- Feature: added buttons to the plugin option page for testing the Discord Webhook & Discord Bot
- Feature: added patterns to define the thread name "$$TARGET$$", "$$DATE$$", "$$DATEMINUS12$$", "$$DATETIME$$", "$$TIME$$")
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