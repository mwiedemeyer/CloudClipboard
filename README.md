# Cloud Clipboard

A simple solution for sharing the clipboard between 2 computers over the Internet. It can be used to share text, images and files from a remote desktop session to your local computer. Even if the remote desktop session does not support clipboard sharing.

## Features

- Share clipboard between 2 computers
- Share text, images and files

## Usage

1. Download the latest release from the [Releases](https://github.com/mwiedemeyer/CloudClipboard/releases) page.
2. Extract the downloaded zip file to a folder of your choice.
3. Open `CloudClipboard.dll.config` and set the required values.
   1. You need an **Azure Storage Account**. Set the connection string in the `StorageConnectionString` field.
   2. Create a blob container and set the name to the `StorageContainer` field.
   3. Create an **Azure Service Bus Basic namespace** and set the connection string to the `ServiceBusConnectionString` field.
   4. Set the local and remote computer names in the `LocalName` and `RemoteName` fields. On each computer, the `LocalName` should be unique and the `RemoteName` should match the `LocalName` of the other computer.
4. Run `CloudClipboard.exe`.
   1. Put a link in `shell:startup` to start the app automatically on login.
5. Copy something on one computer and it should be available on the other computer's clipboard.

## Why? (and why is it a Windows Forms app?)

I had to work on a remote desktop session that did not support clipboard sharing. I wanted a simple solution to share the clipboard between my local computer and the remote desktop session. I could not find any existing solutions that worked for me, so I decided to create my own.

It is a Windows Forms app because it is easy to create a hidden app that runs without a console window. It also makes it easy to add a system tray icon in the future.

## How it works

The application monitors the clipboard for changes. When a change is detected, it uploads the clipboard content to Azure Blob Storage and sends a notification to the remote computer using Azure Service Bus. The remote computer then downloads the clipboard content from Blob Storage and updates its own clipboard. For text content, the text is sent directly via Service Bus without using Blob Storage.

## What are the costs?

The costs are minimal. Azure Blob Storage and Azure Service Bus Basic are both very cheap. The exact costs will depend on the amount of data you transfer and the number of messages you send, but Service Bus Basic costs around $0.05 per million operations and Blob Storage costs around $0.0232 per GB per month plus costs for transactions (read/write).

See the [Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/) for more details.
