﻿/*
 * Copyright 2010-2013 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using Amazon.Runtime;
using Amazon.S3.Util;
using Amazon.Util;
using PCLStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.S3.Model
{
    public partial class GetObjectResponse
    {
        /// <summary>
        /// Writes the content of the ResponseStream a file indicated by the filePath argument.
        /// </summary>
        /// <param name="filePath">The location where to write the ResponseStream</param>
        /// <param name="append">Whether or not to append to the file if it exists</param>
        /// <param name="cancellationToken">Cancellation token which can be used to cancel this operation.</param>
        public async Task WriteResponseStreamToFileAsync(string filePath, bool append, CancellationToken cancellationToken)
        {
            // Make sure the directory exists to write too.
            IFile fi;
            IFolder Directory;
            string dirPath;
            bool fileExists;
            Stream downloadStream;
            
            //**TEW: Change code to make System.IO calls available cross-plat in PCL
            fi = await FileSystem.Current.GetFileFromPathAsync(filePath);
            fileExists = (fi == null); 

            if (fileExists)
            {
                dirPath = fi.Path.Substring(0, fi.Path.LastIndexOf(PortablePath.DirectorySeparatorChar) - 1);
                Directory = await FileSystem.Current.GetFolderFromPathAsync(dirPath);
                List<string> dirhold = dirPath.Split(PortablePath.DirectorySeparatorChar).ToList();
                if (Directory == null) Directory = await FileSystem.Current.LocalStorage.CreateFolderAsync(dirhold.Last(), CreationCollisionOption.OpenIfExists);
                downloadStream = append ? await fi.OpenAsync(FileAccess.ReadAndWrite) : await fi.OpenAsync(FileAccess.Read);
            }
            else
            { 
                List<string> filehold = filePath.Split(PortablePath.DirectorySeparatorChar).ToList();
                downloadStream = await(await FileSystem.Current.LocalStorage.CreateFileAsync(filehold.Last(),CreationCollisionOption.OpenIfExists))
                                       .OpenAsync(FileAccess.ReadAndWrite);
            }

            //FileInfo fi = new FileInfo(filePath);
            //Directory.CreateDirectory(fi.DirectoryName);

            //Stream downloadStream;
            //if (append && File.Exists(filePath))
            //    downloadStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read,S3Constants.DefaultBufferSize);
            //else
            //    downloadStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, S3Constants.DefaultBufferSize);

            try
            {
                long current = 0;
                //Buffered Stream not available in PCL 
                //BufferedStream bufferedStream = new BufferedStream(this.ResponseStream);
                using (Stream bufferedStream = this.ResponseStream) 
                { 
                    
                    byte[] buffer = new byte[S3Constants.DefaultBufferSize];
                    int bytesRead = 0;
                    long totalIncrementTransferred = 0;
                    while ((bytesRead = await bufferedStream.ReadAsync(buffer, 0, buffer.Length)
                        .ConfigureAwait(continueOnCapturedContext:false)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await downloadStream.WriteAsync(buffer, 0, bytesRead)
                            .ConfigureAwait(continueOnCapturedContext: false);
                        current += bytesRead;
                        totalIncrementTransferred += bytesRead;

                        if (totalIncrementTransferred >= AWSSDKUtils.DefaultProgressUpdateInterval ||
                            current == this.ContentLength)
                        {
                            this.OnRaiseProgressEvent(filePath, totalIncrementTransferred, current, this.ContentLength);
                            totalIncrementTransferred = 0;
                        }
                    }

                    ValidateWrittenStreamSize(current);
                }
            }
            finally
            {
                //downloadStream.Close();
                downloadStream.Dispose();
            }
        }
    }
}