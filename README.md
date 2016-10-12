# AzureFileShareOnewaySync

This sample uses the [Microsoft Azure Storage Data Movement Library](https://github.com/Azure/azure-storage-net-data-movement) and the [
Microsoft Azure Storage SDK for .NET](https://github.com/Azure/azure-storage-net) to demonstrate how to keep a secondary [Azure File storage](https://azure.microsoft.com/en-us/services/storage/files/) share in sync with a primary share.

## Features

Using the previously mentioned libraries, this demonstrates how to:

- Copy everything in the source file share to the destination file share
  - Only copy files if the source file is newer than the destination file
- Compare the destination share to source share
    - Delete files / folders in the destination which have been deleted in the source

This sample runs in a console application, but can be repurposed to run in a number of hosts (IaaS VM, WebJob, Worker Role, Azure Automation, etc.). 
## Getting Started

The code uses [transformations](https://msdn.microsoft.com/en-us/library/dd465326(VS.100).aspx) for the app.config.  The app.debug.config is intentionally left out of the repository through .gitignore so that keys, etc. are not stored in the repository.  Once you've cloned the repository or downloaded the code, make sure you add an app.debug.config in the same directory as the app.config and app.release.config.  Once you've done that, you can copy/paste from the app.release.config to the app.debug.config and populate the appropriate values with your source/destination connection strings and shares.

## Deeper Dive 

For a deeper explanation of the scenario this sample aims to address, and walkthrough of the code, please review this [blog post](http://blogs.msdn.com/devkeydet).  TODO: Insert working link.

[@devkeydet](https://twitter.com/devkeydet)