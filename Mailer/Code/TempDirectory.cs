namespace Mailer.Code
{
  using System;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;

  /// <summary>
  /// A temporary directory resource.
  /// </summary>
  public class TempDirectory
  {
    public static async Task<TempDirectory> CreateAsync(
      string root = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      if (root == null)
      {
        root = Path.GetTempPath();
      }

      var directory = new TempDirectory();
      var path = default(string);


      while(true)
      {
        path = Path.Combine(root, Path.GetRandomFileName());

        try
        {
          File.Create(path).Close();

          break;
        }
        catch
        {
          // Continue
        }

        await Task.Delay(10, cancellationToken);
      }

      directory.path = path;

      return directory;
    }

    public async Task CloseAsync(
      CancellationToken cancellationToken = default(CancellationToken))
    {
      if (path == null)
      {
        return;
      }

      while(true)
      {
        try
        {
          Directory.Delete(DirectoryPath, true);

          break;
        }
        catch
        { 
          // Continue.
        }

        await Task.Delay(10, cancellationToken);
      }

      File.Delete(path);
      path = null;

      return;
    }

    /// <summary>
    /// Lock file path.
    /// </summary>
    public String LockPath
    {
      get { return path; }
    }

    /// <summary>
    /// Gets a directory path, or null if TempDirectory instance is closed.
    /// </summary>
    public String DirectoryPath
    {
      get { return path == null ? null : path + ".dir"; }
    }

    /// <summary>
    /// A temp folder.
    /// </summary>
    private String path;
  }
}