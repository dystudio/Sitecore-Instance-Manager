﻿using Sitecore.Diagnostics.Base;
using JetBrains.Annotations;

namespace SIM.FileSystem
{
  public class PathProvider
  {
    #region Fields

    private FileSystem FileSystem { get; }

    #endregion

    #region Constructors

    public PathProvider(FileSystem fileSystem)
    {
      this.FileSystem = fileSystem;
    }

    #endregion

    #region Public methods

    public virtual string EscapePath(string path, string escapeText = null)
    {
      escapeText = escapeText ?? string.Empty;
      return path
        .Replace(":", escapeText)
        .Replace("/", escapeText)
        .Replace("\\", escapeText)
        .Replace("*", escapeText)
        .Replace("|", escapeText)
        .Replace("?", escapeText)
        .Replace("<", escapeText)
        .Replace(">", escapeText);
    }

    [NotNull]
    public virtual string ToUncPath([NotNull] string path)
    {
      Assert.ArgumentNotNullOrEmpty(path, nameof(path));

      const string Prefix = @"\\127.0.0.1\";
      if (path.Length == 1 || path[1] != ':' || path.StartsWith(Prefix))
      {
        return path;
      }

      return Prefix + path.Replace(":", "$");
    }

    #endregion
  }
}