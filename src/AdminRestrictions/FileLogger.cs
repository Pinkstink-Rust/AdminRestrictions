using System;
using System.IO;
using UnityEngine;

namespace AdminRestrictions
{
    internal class FileLogger
    {
        readonly FileInfo _fileInfo;

        float _lastErrorMessageTime = -1;

        public FileLogger(string filePath)
        {
            _fileInfo = new FileInfo(filePath);
            if (!_fileInfo.Directory.Exists) _fileInfo.Directory.Create();
        }

        internal async void Log(string text)
        {
            try
            {
                using (var fs = _fileInfo.Open(FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs))
                {
                    await sw.WriteLineAsync(text);
                    await sw.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                if (Time.realtimeSinceStartup > _lastErrorMessageTime + 1)
                {
                    _lastErrorMessageTime = Time.realtimeSinceStartup;
                    Debug.LogError("Failure writing to log file");
                    Debug.LogException(ex);
                }
            }
        }
    }
}
