#region Copyright
//=======================================================================================
// Microsoft Azure Customer Advisory Team 
//
// This sample is supplemental to the technical guidance published on my personal
// blog at http://blogs.msdn.com/b/paolos/. 
// 
// Author: Paolo Salvatori
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
// 
// LICENSED UNDER THE APACHE LICENSE, VERSION 2.0 (THE "LICENSE"); YOU MAY NOT USE THESE 
// FILES EXCEPT IN COMPLIANCE WITH THE LICENSE. YOU MAY OBTAIN A COPY OF THE LICENSE AT 
// http://www.apache.org/licenses/LICENSE-2.0
// UNLESS REQUIRED BY APPLICABLE LAW OR AGREED TO IN WRITING, SOFTWARE DISTRIBUTED UNDER THE 
// LICENSE IS DISTRIBUTED ON AN "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY 
// KIND, EITHER EXPRESS OR IMPLIED. SEE THE LICENSE FOR THE SPECIFIC LANGUAGE GOVERNING 
// PERMISSIONS AND LIMITATIONS UNDER THE LICENSE.
//=======================================================================================
#endregion

#region Using Directives

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.ServiceBus.Messaging;

#endregion

namespace ServiceBusExplorer.Helpers
{
    public class ZipBrokeredMessageInspector : IBrokeredMessageInspector, IDisposable
    {
        #region IBrokeredMessageInspector Methods
        public BrokeredMessage BeforeSendMessage(BrokeredMessage message)
        {
            var stream = message?.Clone().GetBody<Stream>();
            if (stream == null)
            {
                return null;
            }
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            message.SetBodyStream(Compress(stream));
            return message;
        }

        public BrokeredMessage AfterReceiveMessage(BrokeredMessage message)
        {
            if (message == null)
            {
                return null;
            }
            // Coral: capture the raw compressed bytes (what came off the wire) before
            // decompressing, so the hex viewer can show the on-the-wire payload.
            CaptureRawBody(message);
            var stream = message.Clone().GetBody<Stream>();
            if (stream == null)
            {
                return null;
            }
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            message.SetBodyStream(Decompress(stream));
            return message;
        }
        #endregion

        #region Coral Raw-Body Capture
        private static void CaptureRawBody(BrokeredMessage message)
        {
            try
            {
                using (var raw = message.Clone().GetBody<Stream>())
                {
                    if (raw == null)
                    {
                        return;
                    }
                    if (raw.CanSeek)
                    {
                        raw.Seek(0, SeekOrigin.Begin);
                    }
                    using (var memoryStream = new MemoryStream())
                    {
                        raw.CopyTo(memoryStream);
                        CoralRawBodyCache.Store(message, memoryStream.ToArray());
                    }
                }
            }
            catch (Exception)
            {
                // Capturing the raw body is best-effort; never disrupt message receipt.
            }
        }
        #endregion

        #region IDisposable Methods
        public void Dispose()
        {
        }
        #endregion

        #region Private Static Methods
        private static Stream Compress(Stream inputStream)
        {
            var outputStream = new MemoryStream();
            using (var zipStream = new GZipStream(outputStream, CompressionMode.Compress, true))
            {
                inputStream.CopyTo(zipStream);
            }
            outputStream.Position = 0;
            return outputStream;
        }
        private static Stream Decompress(Stream inputStream)
        {
            var outputStream = new MemoryStream();
            using (var zipStream = new GZipStream(inputStream, CompressionMode.Decompress, true))
            {
                try
                {
                    zipStream.CopyTo(outputStream);
                }
                catch (Exception)
                {
                    return inputStream;
                }
            }
            outputStream.Position = 0;
            return outputStream;
        }
        #endregion
    }
}
