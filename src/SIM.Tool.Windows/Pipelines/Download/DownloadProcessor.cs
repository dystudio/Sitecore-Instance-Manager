﻿namespace SIM.Tool.Windows.Pipelines.Download
{
  using System;
  using System.IO;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using SIM.Pipelines.Processors;
  using SIM.Products;
  using SIM.Tool.Base;
  using Sitecore.Diagnostics.Base;
  using Sitecore.Diagnostics.Logging;
  using SIM.Extensions;

  public class DownloadProcessor : Processor
  {
    #region Public methods

    public void DownloadFile(Uri url, string fileName, long fileSize, string localRepository, string cookies, CancellationToken token)
    {
      using (var response = WebRequestHelper.RequestAndGetResponse(url, null, null, cookies))
      {
        var destFileName = Path.Combine(localRepository, fileName);
        Assert.IsTrue(!FileSystem.FileSystem.Local.File.Exists(destFileName), "The {0} file already exists".FormatWith(destFileName));

        if (TryCopyFromExternalRepository(fileName, destFileName))
        {
          Controller.IncrementProgress(fileSize);
          return;
        }

        WebRequestHelper.DownloadFile(url, destFileName, response, token, count => Controller.IncrementProgress(count));
      }
    }

    public override long EvaluateStepsCount(ProcessorArgs args)
    {
      return ((DownloadArgs)args)._Sizes.Sum(size => size.Value);
    }

    #endregion

    #region Protected methods

    protected override void Process(ProcessorArgs args)
    {
      var download = (DownloadArgs)args;
      var cookies = download.Cookies;
      var localRepository = download.LocalRepository;
      var fileNames = download._FileNames;
      Assert.IsNotNull(fileNames, nameof(fileNames));
      
      var links = download._Links;
      var fileSizes = download._Sizes;
      Assert.IsNotNull(fileSizes, nameof(fileSizes));

      var parallelDownloadsNumber = WindowsSettings.AppDownloaderParallelThreads.Value;

      var cancellation = new CancellationTokenSource();
      var urls = links.Where(link => link != null && RequireDownloading(fileNames[link], fileSizes[link], localRepository)).ToArray();
      for (int i = 0; i < urls.Length; i += parallelDownloadsNumber)
      {
        var remains = urls.Length - i;
        var tasks = urls
          .Skip(i)
          .Take(Math.Min(parallelDownloadsNumber, remains))
          .Where(url => url != null)
          .Select(url => Task.Factory.StartNew(() => DownloadFile(url, fileNames[url], fileSizes[url], localRepository, cookies, cancellation.Token), cancellation.Token))
          .ToArray();

        try
        {
          Task.WaitAll(tasks, WindowsSettings.AppDownloaderTotalTimeout.Value * WebRequestHelper.Hour);
        }
        catch (Exception ex)
        {
          Log.Warn(ex, "An error occurred during downloading files");

          cancellation.Cancel();
          throw;
        }
      }
    }

    #endregion

    #region Private methods

    private bool RequireDownloading(string fileName, long fileSize, string localRepository)
    {
      var filePath1 = ProductManager.Products.Select(product => product.PackagePath).FirstOrDefault(packagePath => Path.GetFileName(packagePath).EqualsIgnoreCase(fileName));
      if (!string.IsNullOrEmpty(filePath1))
      {
        if (FileSystem.FileSystem.Local.File.Exists(filePath1))
        {
          if (FileSystem.FileSystem.Local.File.GetFileLength(filePath1) == fileSize)
          {
            Log.Info($"Downloading is skipped, the {filePath1} file already exists");
            return false;
          }

          Log.Info($"The {filePath1} file already exists, but its size is incorrect - file will be removed");
          FileSystem.FileSystem.Local.File.Delete(filePath1);
        }
      }

      var filePath2 = Path.Combine(localRepository, fileName);
      if (FileSystem.FileSystem.Local.File.Exists(filePath2))
      {
        if (FileSystem.FileSystem.Local.File.GetFileLength(filePath2) == fileSize)
        {
          Log.Info($"Downloading is skipped, the {filePath2} file already exists");
          return false;
        }

        Log.Info($"The {filePath2} file already exists, but its size is incorrect - file will be removed");
        FileSystem.FileSystem.Local.File.Delete(filePath2);
      }

      return true;
    }

    private bool TryCopyFromExternalRepository(string fileName, string destFileName)
    {
      var externalRepositories = WindowsSettings.AppDownloaderExternalRepository.Value;
      if (!string.IsNullOrEmpty(externalRepositories))
      {
        try
        {
          foreach (var repository in externalRepositories.Split('|').Reverse())
          {
            var files = FileSystem.FileSystem.Local.Directory.GetFiles(repository, fileName, SearchOption.AllDirectories);
            var externalRepositoryFilePath = files.FirstOrDefault();
            if (!string.IsNullOrEmpty(externalRepositoryFilePath) && FileSystem.FileSystem.Local.File.Exists(externalRepositoryFilePath))
            {
              using (new ProfileSection("Copying file from remote repository", this))
              {
                ProfileSection.Argument("fileName", fileName);
                ProfileSection.Argument("externalRepositoryFilePath", externalRepositoryFilePath);

                WindowHelper.CopyFileUi(externalRepositoryFilePath, destFileName, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs, Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
              }

              Log.Info($"Copying the {fileName} file has completed");
              return true;
            }
          }
        }
        catch (Exception ex)
        {
          Log.Warn(ex, $"Unable to copy the {fileName} file from external repository");
        }
      }

      return false;
    }

    #endregion
  }
}
