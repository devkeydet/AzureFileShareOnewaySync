using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.DataMovement;
using Microsoft.WindowsAzure.Storage.File;

namespace AzureFileShareOnewaySync
{
    internal class Program
    {
        private static void Main()
        {
            //Increase .NET HTP connections limit per https://github.com/Azure/azure-storage-net-data-movement#best-practice
            ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount * 8;

            // Turn off 100 -continue per https://github.com/Azure/azure-storage-net-data-movement#best-practice
            ServicePointManager.Expect100Continue = false;

            var (sourceStorageAccount, sourceDir, destStorageAccount, destDir) = InitiliazeFromConfig();

            CopyNewerSourceFilesToDestination(sourceStorageAccount, sourceDir, destStorageAccount, destDir);

            DeleteDestinationFilesAndDirectoriesNoLongerInSource(destDir, sourceStorageAccount);
        }

        private static (CloudStorageAccount, CloudFileDirectory, CloudStorageAccount, CloudFileDirectory)
            InitiliazeFromConfig()
        {
            var sourceStorageConnectionString = ConfigurationManager.AppSettings["SourceConnectionString"];
            var sourceStorageAccount = CloudStorageAccount.Parse(sourceStorageConnectionString);
            var sourceFileClient = sourceStorageAccount.CreateCloudFileClient();
            var sourceShare = sourceFileClient.GetShareReference(ConfigurationManager.AppSettings["SourceShare"]);
            var sourceDir = sourceShare.GetRootDirectoryReference();

            var destStorageConnectionString = ConfigurationManager.AppSettings["DestinationConnectionString"];
            var destStorageAccount = CloudStorageAccount.Parse(destStorageConnectionString);
            var destFileClient = destStorageAccount.CreateCloudFileClient();
            var destShare = destFileClient.GetShareReference(ConfigurationManager.AppSettings["DestinationShare"]);
            var destDir = destShare.GetRootDirectoryReference();

            return (sourceStorageAccount, sourceDir, destStorageAccount, destDir);
        }

        private static void CopyNewerSourceFilesToDestination(
            CloudStorageAccount sourceStorageAccount,
            CloudFileDirectory sourceDir,
            CloudStorageAccount destStorageAccount,
            CloudFileDirectory destDir)
        {
            var transferContext = new TransferContext();

            transferContext.FileFailed += (sender, args) => { Console.WriteLine(args.Exception.Message); };

            transferContext.FileSkipped += (sender, args) =>
            {
                //Console.WriteLine(args.Exception.Message);
            };

            transferContext.FileTransferred += (sender, args) =>
            {
                Console.WriteLine($"Completed transferring {args.Destination}");
            };

            transferContext.OverwriteCallback = (source, destination) =>
            {
                // By default, files are not overwritten.  We want to overwrite files when the source has a newer file than the destination.
                var sourceFile = new CloudFile(new Uri(source), sourceStorageAccount.Credentials);
                var destFile = new CloudFile(new Uri(destination), destStorageAccount.Credentials);

                // Execute in parallel
                var sourceFileFetchAttributesTask = Task.Run(() => sourceFile.FetchAttributes());
                var destFileFetchAttributesTask = Task.Run(() => destFile.FetchAttributes());

                Task.WaitAll(sourceFileFetchAttributesTask, destFileFetchAttributesTask);

                return sourceFile.Properties.LastModified > destFile.Properties.LastModified;
            };

            var options = new CopyDirectoryOptions
            {
                Recursive = true
            };

            TransferManager.CopyDirectoryAsync(sourceDir, destDir, true, options, transferContext).Wait();
        }

        private static void DeleteDestinationFilesAndDirectoriesNoLongerInSource(
            CloudFileDirectory destDir,
            CloudStorageAccount sourceStorageAccount)
        {
            var sourceEndpoint = sourceStorageAccount.FileEndpoint.ToString().TrimEnd('/');
            var filesAndDirectories = ListFilesAndDirectoriesAsync(destDir).Result;
            Parallel.ForEach(filesAndDirectories, fileItem =>
            {
                var cloudFile = fileItem as CloudFile;
                if (cloudFile != null)
                {
                    var sourceFile = new CloudFile(
                        new Uri($"{sourceEndpoint}{fileItem.Uri.AbsolutePath}"),
                        sourceStorageAccount.Credentials);

                    if (sourceFile.Exists()) return;

                    cloudFile.Delete();
                    Console.WriteLine($"Deleted: {cloudFile.Uri}");
                }
                else
                {
                    var cloudFileDirectory = fileItem as CloudFileDirectory;

                    if (cloudFileDirectory == null) return;

                    DeleteDestinationFilesAndDirectoriesNoLongerInSource(cloudFileDirectory, sourceStorageAccount);

                    if (cloudFileDirectory.ListFilesAndDirectories().Count() != 0) return;

                    cloudFileDirectory.Delete();
                    Console.WriteLine($"Deleted directory: {cloudFileDirectory.Uri}");
                }
            });
        }

        private static async Task<List<IListFileItem>> ListFilesAndDirectoriesAsync(CloudFileDirectory destDir)
        {
            FileContinuationToken continuationToken = null;
            var results = new List<IListFileItem>();
            do
            {
                var response = await destDir.ListFilesAndDirectoriesSegmentedAsync(continuationToken)
                    .ConfigureAwait(false);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            } while (continuationToken != null);

            return results;
        }
    }
}