// IPA-DN-FileCenter
// 
// Copyright (c) 2020- IPA CyberLab.
// All Rights Reserved.
// 
// License: The Apache License, Version 2.0
// https://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// 
// THIS SOFTWARE IS DEVELOPED IN JAPAN, AND DISTRIBUTED FROM JAPAN, UNDER
// JAPANESE LAWS. YOU MUST AGREE IN ADVANCE TO USE, COPY, MODIFY, MERGE, PUBLISH,
// DISTRIBUTE, SUBLICENSE, AND/OR SELL COPIES OF THIS SOFTWARE, THAT ANY
// JURIDICAL DISPUTES WHICH ARE CONCERNED TO THIS SOFTWARE OR ITS CONTENTS,
// AGAINST US (IPA CYBERLAB, DAIYUU NOBORI, SOFTETHER VPN PROJECT OR OTHER
// SUPPLIERS), OR ANY JURIDICAL DISPUTES AGAINST US WHICH ARE CAUSED BY ANY KIND
// OF USING, COPYING, MODIFYING, MERGING, PUBLISHING, DISTRIBUTING, SUBLICENSING,
// AND/OR SELLING COPIES OF THIS SOFTWARE SHALL BE REGARDED AS BE CONSTRUED AND
// CONTROLLED BY JAPANESE LAWS, AND YOU MUST FURTHER CONSENT TO EXCLUSIVE
// JURISDICTION AND VENUE IN THE COURTS SITTING IN TOKYO, JAPAN. YOU MUST WAIVE
// ALL DEFENSES OF LACK OF PERSONAL JURISDICTION AND FORUM NON CONVENIENS.
// PROCESS MAY BE SERVED ON EITHER PARTY IN THE MANNER AUTHORIZED BY APPLICABLE
// LAW OR COURT RULE.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

using IPA.Cores.Web;
using IPA.Cores.Helper.Web;
using static IPA.Cores.Globals.Web;

using IPA.Cores.Codes;
using IPA.Cores.Helper.Codes;
using static IPA.Cores.Globals.Codes;

using IPA.DN.FileCenter;
using IPA.DN.FileCenter.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace IPA.DN.FileCenter
{
    public class FileCenterConsts
    {
        public const string DefaultStoreRoot = "./Local/DataRoot/";
        public const string SingleInstanceKey = "IPA.DN.FileCenter";
        public const string HiveSettingsName = "FileCenter/Settings";

        public const string FileBrowserHttpDir = "/d";

        public const string DefaultWebSiteTitle = "FileCenter";

        public const int MaxUploadFiles = 10;

        public const int MaxUrlHintLen = 32;
    }

    public class DbHive : INormalizable
    {
        public string DataStoreRootDir = "";
        public string PIN = "";
        public string WebSiteTitle = "";
        public DateTimeOffset LastUploadDateTime;
        public int LastSeqNo;

        public void Normalize()
        {
            if (DataStoreRootDir._IsEmpty()) DataStoreRootDir = FileCenterConsts.DefaultStoreRoot;

            if (WebSiteTitle._IsEmpty()) WebSiteTitle = FileCenterConsts.DefaultWebSiteTitle;

            if (LastUploadDateTime._IsZeroDateTime()) LastUploadDateTime = Util.ZeroDateTimeOffsetValue;
        }
    }

    public class UploadResult
    {
        public string GeneratedUrlDir { get; set; } = "";
        public string GeneratedUrlDirDirect { get; set; } = "";
        public string? GeneratedUserName { get; set; }
        public string? GeneratedPassword { get; set; }
        public bool IsZipped { get; set; }
        public string? GeneratedZipPassword { get; set; }
        public string Destination { get; set; } = "";
        public DateTimeOffset Expires { get; set; } = Util.MaxDateTimeOffsetValue;
        public int NumFiles { get; set; }
        public long TotalFileSize { get; set; }
    }

    public class UploadFormRequest
    {
        public string? Destination { get; set; }
        public string? UrlHint { get; set; }
        public bool Auth { get; set; }
        public bool Log { get; set; }
        public bool Zip { get; set; }
        public bool Once { get; set; }
        public int Days { get; set; }

        public string? dirname_1 { get; set; }
        public string? dirname_2 { get; set; }
        public string? dirname_3 { get; set; }
        public string? dirname_4 { get; set; }
        public string? dirname_5 { get; set; }
        public string? dirname_6 { get; set; }
        public string? dirname_7 { get; set; }
        public string? dirname_8 { get; set; }
        public string? dirname_9 { get; set; }
        public string? dirname_10 { get; set; }
    }

    public class UploadFile : AsyncService
    {
        public Stream Stream { get; }
        public string RelativeFileName { get; }

        public UploadFile(Stream fileStream, string fileName, string? subDirName = null)
        {
            try
            {
                fileName = PPWin.GetFileName(fileName);

                if (fileName._IsNullOrZeroLen()) throw new ArgumentNullException(nameof(fileName));

                subDirName = subDirName._NonNullTrim();

                subDirName = subDirName._MakeSafeFileName();

                this.RelativeFileName = (subDirName._IsEmpty() ? "" : subDirName + "/") + fileName;

                this.Stream = fileStream;
            }
            catch
            {
                this._DisposeSafe();
                throw;
            }
        }

        protected override async Task CleanupImplAsync(Exception? ex)
        {
            try
            {
                await this.Stream._DisposeSafeAsync();
            }
            finally
            {
                await base.CleanupImplAsync(ex);
            }
        }
    }

    public class UploadFileList : AsyncService
    {
        readonly List<UploadFile> FileListInternal = new List<UploadFile>();

        public IReadOnlyList<UploadFile> FileList => FileListInternal;

        public void AddFormFileList(IEnumerable<IFormFile?>? files, string? subDirName)
        {
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file != null)
                    {
                        UploadFile f = new UploadFile(file.OpenReadStream(), file.FileName, subDirName);

                        AddFileInternal(f);
                    }
                }
            }
        }

        public void AddFile(Stream fileStream, string fileName, string? subDirName = null)
        {
            UploadFile f = new UploadFile(fileStream, fileName, subDirName);

            AddFileInternal(f);
        }

        void AddFileInternal(UploadFile f)
        {
            this.FileListInternal.Add(f);
        }

        protected override async Task CleanupImplAsync(Exception? ex)
        {
            try
            {
            }
            finally
            {
                await base.CleanupImplAsync(ex);
            }
        }
    }

    public class UploadOption : INormalizable
    {
        public string? Destination { get; set; }
        public string? UrlHint { get; set; }
        public bool Auth { get; set; }
        public bool LogAccess { get; set; }
        public bool Zip { get; set; }
        public bool Once { get; set; }
        public int Days { get; set; }

        public string IpAddress { get; set; } = "";

        public void Normalize()
        {
            this.Destination = this.Destination._NonNullTrimSe()._TruncStr(Consts.MaxLens.NormalStringTruncateLen);
            this.UrlHint = this.UrlHint._Normalize(true, true, false, false);

            StringBuilder sb = new StringBuilder();
            foreach (char c in UrlHint)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || (c == '-' || c == '_'))
                {
                    sb.Append(c);
                }
            }

            this.UrlHint = sb.ToString()._TruncStr(FileCenterConsts.MaxUrlHintLen);

            if (this.Days < 0) this.Days = 0;
            if (this.Days > 2555000) this.Days = 2555000;

            this.IpAddress = this.IpAddress._NonNull();
        }
    }

    public class Server : AsyncService
    {
        readonly SingleInstance SingleInstance;

        // Hive ベースのデータベース
        readonly HiveData<DbHive> HiveData;

        // データベースへのアクセスを容易にするための自動プロパティ
        public CriticalSection DbLock => HiveData.DataLock;
        public DbHive Db => HiveData.ManagedData;
        public DbHive DbSnapshot => HiveData.GetManagedDataSnapshot();

        public string RootDirectoryFullPath => Path.Combine(Env.AppRootDir, DbSnapshot.DataStoreRootDir);

        readonly LogBrowser Browser;

        public Server(CancellationToken cancel = default) : base(cancel)
        {
            try
            {
                // 多重起動を防止
                this.SingleInstance = new SingleInstance(FileCenterConsts.SingleInstanceKey);

                // 設定データベースの初期化
                this.HiveData = new HiveData<DbHive>(Hive.SharedLocalConfigHive, FileCenterConsts.HiveSettingsName,
                    getDefaultDataFunc: () => new DbHive(),
                    policy: HiveSyncPolicy.AutoReadWriteFile,
                    serializer: HiveSerializerSelection.RichJson);

                // 設定データベースに記載されているディレクトリを作成
                CreateRootDirectory();

                Browser = new LogBrowser(new LogBrowserOptions(this.RootDirectoryFullPath,
                    systemTitle: this.DbSnapshot.WebSiteTitle,
                    flags: LogBrowserFlags.NoPreview | LogBrowserFlags.NoRootDirectory | LogBrowserFlags.SecureJson), FileCenterConsts.FileBrowserHttpDir);

                this.HiveData.EventListener.RegisterCallback(async (caller, type, state) =>
                {
                    await SettingsUpdateAsync();
                });

                SettingsUpdateAsync()._GetResult();
            }
            catch
            {
                this._DisposeSafe();
                throw;
            }
        }

        // 設定内容が変化したときに呼び出される
        async Task SettingsUpdateAsync()
        {
            await Task.CompletedTask;

            var data = this.DbSnapshot;

            this.Browser.Options.SetSystemTitle(data.WebSiteTitle);
        }

        public async Task<HttpResult> ProcessFileBrowserRequestAsync(IPAddress clientIpAddress, int clientPort, string requestPathAndQueryString, HttpRequest request, HttpResponse response, CancellationToken cancel = default)
        {
            return await this.Browser.ProcessRequestAsync(clientIpAddress, clientPort, requestPathAndQueryString, request, response, cancel);
        }

        readonly AsyncLock DirectoryLock = new AsyncLock();

        // アップロードメイン処理
        public async Task<ResultOrExeption<UploadResult>> UploadAsync(DateTimeOffset timeStamp, string ipAddress, string baseUrl, UploadFileList fileList, UploadOption option, CancellationToken cancel = default)
        {
            option.Normalize();

            string newDirName;
            string newDirFullPath;

            using (await DirectoryLock.LockWithAwait(cancel))
            {
                int seqNo;

                lock (this.DbLock)
                {
                    if (this.Db.LastUploadDateTime.LocalDateTime.Date != timeStamp.LocalDateTime.Date)
                    {
                        // 日が変わった
                        this.Db.LastSeqNo = 0;
                    }
                    this.Db.LastUploadDateTime = timeStamp;

                    // 連番をインクリメントいたします
                    seqNo = ++this.Db.LastSeqNo;
                }

                for (int i = 0; ; i++)
                {
                    string yymmddAndSeqNo = timeStamp.LocalDateTime.ToString("yyMMdd") + "_" + seqNo.ToString("D3");

                    int sizeOfRandStr = 16;

                    // ユニークなルートディレクトリ名を決定する
                    if (option.UrlHint._IsFilled())
                    {
                        sizeOfRandStr = 16 - option.UrlHint.Length - 1;
                    }

                    sizeOfRandStr = Math.Max(sizeOfRandStr, 8);

                    string candidate = yymmddAndSeqNo + "_";
                    if (option.UrlHint._IsFilled())
                    {
                        candidate += option.UrlHint + "_";
                    }

                    candidate += Str.GenRandPassword(sizeOfRandStr, false).ToLower();

                    string fullPath = PP.Combine(this.RootDirectoryFullPath, candidate);

                    if (await Lfs.IsDirectoryExistsAsync(fullPath, cancel) ||
                        await Lfs.IsFileExistsAsync(fullPath, cancel))
                    {
                        // すでに存在する
                    }
                    else
                    {
                        // 決定 !!
                        newDirName = candidate;
                        newDirFullPath = fullPath;
                        break;
                    }
                }
            }

            newDirFullPath._Debug();

            // パスワード等を生成
            UploadResult result = new UploadResult
            {
                Destination = option.Destination._NonNull(),
            };

            if (option.Days <= 0)
            {
                result.Expires = Util.MaxDateTimeOffsetValue;
            }
            else
            {
                result.Expires = timeStamp.LocalDateTime.AddDays(option.Days).AddDays(1).AddSeconds(-1)._AsDateTimeOffset(true);
            }

            if (option.Auth)
            {
                result.GeneratedUserName = Str.GenRandPassword(8, false);
                result.GeneratedPassword = Str.GenRandPassword(24, true);
            }

            if (option.Zip)
            {
                result.GeneratedZipPassword = Str.GenRandPassword(32, true);
                result.IsZipped = true;
            }

            // ディレクトリ作成
            await Lfs.CreateDirectoryAsync(newDirFullPath, flags: FileFlags.OnCreateSetCompressionFlag, cancel: cancel);

            if (false)
            {
                // 暗号化 ZIP なしの場合、アップロードされてきたファイルを生ファイルシステムにそのまま書いていく
                foreach (var file in fileList.FileList)
                {
                    string newFileFullPath = Lfs.PP.Combine(newDirFullPath, file.RelativeFileName);
                    if (option.Auth)
                    {
                        newFileFullPath += Consts.Extensions.EncryptedXtsAes256;
                    }

                    using var newFileObj = await Lfs.CreateAsync(newFileFullPath, flags: FileFlags.AutoCreateDirectory, doNotOverwrite: true, cancel: cancel);
                    using Stream newFileStream = option.Auth == false ? (Stream)newFileObj.GetStream(true) : new XtsAesRandomAccess(newFileObj, result.GeneratedPassword!, true).GetStream(true);
                    await file.Stream.CopyBetweenStreamAsync(newFileStream, cancel: cancel, flush: true);
                }
            }
            else
            {
                // 暗号化 ZIP ありの場合、すべてのファイルを ZIP で圧縮・暗号化した上で 1 つの ZIP ファイルとして保存する
                string zipFileFullPath = Lfs.PP.Combine(newDirFullPath, "test.zip");
                if (option.Auth)
                {
                    zipFileFullPath += Consts.Extensions.EncryptedXtsAes256;
                }

                using var zipOutputFile = await Lfs.CreateAsync(zipFileFullPath, flags: FileFlags.AutoCreateDirectory, doNotOverwrite: true, cancel: cancel);
                using IRandomAccess<byte> zipOutputFileRandomAccess = option.Auth == false ? (IRandomAccess<byte>)zipOutputFile.GetStream(true) : new XtsAesRandomAccess(zipOutputFile, result.GeneratedPassword!, true);
                using var zipWriter = new ZipWriter(new ZipContainerOptions(zipOutputFileRandomAccess));

                foreach (var file in fileList.FileList)
                {
                    var metadata = new FileMetadata(attributes: FileAttributes.Normal, creationTime: timeStamp, lastWriteTime: timeStamp, lastAccessTime: timeStamp);

                    await zipWriter.ImportVirtualFileAsync(file.Stream,
                        new FileContainerEntityParam(file.RelativeFileName, metadata,
                            FileContainerEntityFlags.EnableCompression | FileContainerEntityFlags.CompressionMode_Fast,
                        encryptPassword: "a"), cancel);
                }

                await zipWriter.FinishAsync(cancel);
            }

            return new ResultOrExeption<UploadResult>(result);
        }

        // 設定データベースに記載されているディレクトリを作成
        public void CreateRootDirectory()
        {
            try
            {
                Lfs.CreateDirectory(this.RootDirectoryFullPath);
            }
            catch { }
        }

        protected override void DisposeImpl(Exception? ex)
        {
            try
            {
                this.Browser._DisposeSafe();

                this.HiveData._DisposeSafe();

                this.SingleInstance._DisposeSafe();
            }
            finally
            {
                base.DisposeImpl(ex);
            }
        }
    }
}


