using System;
using System.IO;
using SMBLibrary;
using SMBLibrary.Client;
using FileAttributes = SMBLibrary.FileAttributes;

namespace TagReporter.Utils;

public class SmbLibUtils
{
    public static NTStatus CreateDir(ISMBFileStore fileStore, string path)
    {
        object handle;
        FileStatus fileStatus;
        var status = fileStore.CreateFile(out handle, out fileStatus,
            path, AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
            FileAttributes.Directory,
            ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
            CreateDisposition.FILE_OPEN_IF,
            CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
            null);
        fileStore.CloseFile(handle);
        return status;
    }

    public static NTStatus CreateFile(ISMBClient client, ISMBFileStore fileStore, FileStream stream, string filePath)
    {
        FileStatus fileStatus;
        var status = fileStore.CreateFile(out var fileHandle, out fileStatus, filePath,
            AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
            FileAttributes.Normal,
            ShareAccess.None,
            CreateDisposition.FILE_OVERWRITE_IF,
            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
            null
        );
        if (status != NTStatus.STATUS_SUCCESS) return status;
        var writeOffset = 0;
        while (stream.Position < stream.Length)
        {
            var buffer = new byte[client.MaxWriteSize];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead < client.MaxWriteSize) 
                Array.Resize(ref buffer, bytesRead);
            status = fileStore.WriteFile(out var numberOfBytesWritten, fileHandle, writeOffset, buffer);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new Exception("failed to write to file");
            writeOffset += numberOfBytesWritten;
        }
        status = fileStore.CloseFile(fileHandle);
        return status;
    }
}