using System;
using System.Net;
using Microsoft.WindowsAzure.Storage.DataMovement;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace AzureFileShareOnewaySync
{
    class Program
    {
        static void Main(string[] args)
        {
            //Increase .NET HTP connections limit per https://github.com/Azure/azure-storage-net-data-movement#best-practice
            ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount * 8;

            // Turn off 100 -continue per https://github.com/Azure/azure-storage-net-data-movement#best-practice
            ServicePointManager.Expect100Continue = false;

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

            CopyNewerSourceFilesToDestination(sourceStorageAccount, sourceDir, destStorageAccount, destDir);

            DeleteDestinationFilesAndDirectoriesNoLongerInSource(destDir, sourceStorageAccount);
        }

        private static TransferStatus CopyNewerSourceFilesToDestination(CloudStorageAccount sourceStorageAccount, CloudFileDirectory sourceDir, CloudStorageAccount destStorageAccount, CloudFileDirectory destDir)
        {
            var transferContext = new TransferContext();

            transferContext.FileFailed += new EventHandler<TransferEventArgs>((sender, args) =>
            {
                Console.WriteLine(args.Exception.Message);
            });

            transferContext.FileSkipped += new EventHandler<TransferEventArgs>((sender, args) =>
            {
                //Console.WriteLine(args.Exception.Message);
            });

            transferContext.FileTransferred += new EventHandler<TransferEventArgs>((sender, args) =>
            {
                Console.WriteLine($"Completed transferring {args.Destination}");
            });

            transferContext.OverwriteCallback = (source, destination) =>
            {
                // By default, files are not overwritten.  We want to overwrite files when the source has a newer file than the destination.
                var sourceFile = new CloudFile(new Uri(source), sourceStorageAccount.Credentials);
                sourceFile.FetchAttributes();
                var destFile = new CloudFile(new Uri(destination), destStorageAccount.Credentials);
                destFile.FetchAttributes();

                if (sourceFile.Properties.LastModified > destFile.Properties.LastModified)
                {
                    return true;
                }

                return false;
            };

            var options = new CopyDirectoryOptions
            {
                Recursive = true
            };

            var task = TransferManager.CopyDirectoryAsync(sourceDir, destDir, true, options, transferContext);
            task.Wait();

            return task.Result;
        }

        private static void DeleteDestinationFilesAndDirectoriesNoLongerInSource(CloudFileDirectory destDir, CloudStorageAccount sourceStorageAccount)
        {
            var sourceEndpoint = sourceStorageAccount.FileEndpoint.ToString().TrimEnd('/');
            var filesAndDirectories = destDir.ListFilesAndDirectories();
            Parallel.ForEach(filesAndDirectories, (fileItem) =>
            {
                var cloudFile = fileItem as CloudFile;
                if (cloudFile != null)
                {
                    var sourceFile = new CloudFile(new Uri($"{sourceEndpoint}{fileItem.Uri.AbsolutePath}"), sourceStorageAccount.Credentials);
                    if (!sourceFile.Exists())
                    {
                        cloudFile.Delete();
                        Console.WriteLine($"Deleted: {cloudFile.Uri}");
                    }
                }
                else
                {
                    var cloudFileDirectory = fileItem as CloudFileDirectory;
                    if (cloudFileDirectory != null)
                    {
                        DeleteDestinationFilesAndDirectoriesNoLongerInSource(cloudFileDirectory, sourceStorageAccount);
                        if (cloudFileDirectory.ListFilesAndDirectories().Count() == 0)
                        {
                            cloudFileDirectory.Delete();
                            Console.WriteLine($"Deleted directory: {cloudFileDirectory.Uri}");
                        }
                    }
                }
            });
            //foreach (var fileItem in filesAndDirectories)
            //{
            //    var cloudFile = fileItem as CloudFile;
            //    if (cloudFile != null)
            //    {
            //        var sourceFile = new CloudFile(new Uri($"{sourceEndpoint}{fileItem.Uri.AbsolutePath}"), sourceStorageAccount.Credentials);
            //        if (!sourceFile.Exists())
            //        {
            //            cloudFile.Delete();
            //            Console.WriteLine($"Deleted: {cloudFile.Uri}");
            //        }        
            //    }
            //    else
            //    {
            //        var cloudFileDirectory = fileItem as CloudFileDirectory;
            //        if (cloudFileDirectory != null)
            //        {                   
            //            DeleteDestinationFilesAndDirectoriesNoLongerInSource(cloudFileDirectory, sourceStorageAccount);
            //            if (cloudFileDirectory.ListFilesAndDirectories().Count() == 0)
            //            {
            //                cloudFileDirectory.Delete();
            //                Console.WriteLine($"Deleted directory: {cloudFileDirectory.Uri}");
            //            }
            //        }
            //    }                
            //}
        }
    }
}
