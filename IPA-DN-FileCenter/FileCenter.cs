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
using System.ComponentModel.DataAnnotations;

namespace IPA.DN.FileCenter
{
    public class FileCenterConsts
    {
        public const string DefaultStoreRoot = "./Local/DataRoot/";
        public const string SingleInstanceKey = "IPA.DN.FileCenter";
        public const string HiveSettingsName = "FileCenter/Settings";

        public const string FileBrowserHttpDir = "/d";

        public const string DefaultWebSiteTitle = "IPA FileCenter Secure File Upload System";

        public const int MaxUploadFileFormElements = 10;

        public const int MaxUrlHintLen = 32;

        // 最大アップロード可能容量
        public const long UploadSizeHardLimit = 100L * 1024 * 1024 * 1024; // 100GB

        // デフォルトアップロード可能容量
        public const long DefaultUploadSizeLimit = 2L * 1024 * 1024 * 1024; // 2GB

        // デフォルトアップロード可能ファイル数
        public const int DefaultUploadNumLimit = 100;
    }

    public class AppSettings : INormalizable, IValidatableObject, IValidatable
    {
        [Display(Name = "Web サイト表示名")]
        [Required]
        public string WebSiteTitle { get; set; } = "";

        [Display(Name = "PIN コード")]
        public string? PIN { get; set; }

        [Display(Name = "一度にアップロード可能なファイルの合計サイズ")]
        public long UploadSizeLimit { get; set; }

        [Display(Name = "一度にアップロード可能なファイルの数")]
        public int UploadNumLimit { get; set; }

        [Display(Name = "SMTP サーバーホスト名")]
        public string SmtpHostname { get; set; } = "";

        [Display(Name = "SMTP サーバーポート番号")]
        public int SmtpPort { get; set; } = 0;

        [Display(Name = "SMTP サーバーユーザー名")]
        public string? SmtpUsername { get; set; }

        [Display(Name = "SMTP サーバーパスワード")]
        public string? SmtpPassword { get; set; }

        public string DataStoreRootDir { get; set; } = "";
        public DateTimeOffset LastUploadDateTime { get; set; }

        public int LastSeqNo { get; set; }

        public void Normalize()
        {
            if (DataStoreRootDir._IsEmpty()) DataStoreRootDir = FileCenterConsts.DefaultStoreRoot;

            if (WebSiteTitle._IsEmpty()) WebSiteTitle = FileCenterConsts.DefaultWebSiteTitle;

            if (LastUploadDateTime._IsZeroDateTime()) LastUploadDateTime = Util.ZeroDateTimeOffsetValue;

            if (UploadSizeLimit <= 0) UploadSizeLimit = FileCenterConsts.DefaultUploadSizeLimit;
            UploadSizeLimit = Math.Min(UploadSizeLimit, FileCenterConsts.UploadSizeHardLimit);

            if (UploadNumLimit <= 0) UploadNumLimit = FileCenterConsts.DefaultUploadNumLimit;

            if (this.SmtpPort == 0) this.SmtpPort = Consts.Ports.Smtp;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            => this._Validate(validationContext);

        public void Validate()
        {
        }
    }

    public class UploadResult
    {
        public string GeneratedUrlDir { get; set; } = "";
        public string GeneratedUrlDirAuthDirect { get; set; } = "";
        public string GeneratedUrlFirstFileDirect { get; set; } = "";
        public string GeneratedUrlDirAuthCredentialDirect { get; set; } = "";
        public string GeneratedUrlFirstFileAuthCredentialDirect { get; set; } = "";
        public string? GeneratedUserName { get; set; }
        public string? GeneratedPassword { get; set; }
        public bool IsZipped { get; set; }
        public string? GeneratedZipPassword { get; set; }
        public string Recipient { get; set; } = "";
        public DateTimeOffset Expires { get; set; } = Util.MaxDateTimeOffsetValue;
        public int NumFiles { get; set; }
        public long TotalFileSize { get; set; }
        public bool AllowOnlyOnce { get; set; }
        public string FirstFileNameForPrint { get; set; } = "";

        public override string ToString()
        {
            StringWriter w = new StringWriter();

            w.WriteLine("--------- ファイルの送信のご案内 ここから ----------");

            if (this.Recipient._IsFilled())
            {
                w.WriteLine($"{this.Recipient.Trim()} 様");
                w.WriteLine();
            }

            if (this.NumFiles == 1)
            {
                w.WriteLine($"ファイル 「{this.FirstFileNameForPrint}」 ({this.TotalFileSize._GetFileSizeStr()}) をお送りいたします。");
            }
            else
            {
                w.WriteLine($"ファイル 「{this.FirstFileNameForPrint}」 等 {this.NumFiles._ToString3()} ファイル (合計 {this.TotalFileSize._GetFileSizeStr()}) をお送りいたします。");
            }

            w.WriteLine("大変お手数ですが、以下の URL にアクセスの上、ダウンロードをお願いいたします。");
            w.WriteLine();

            w.WriteLine("■ ファイルをダウンロードするための URL:");
            w.WriteLine(GeneratedUrlDir);
            w.WriteLine();

            if (this.GeneratedUserName._IsFilled())
            {
                w.WriteLine("※ この URL は第三者に配布・転載しないでください。");
                w.WriteLine();

                w.WriteLine("■ 上記 URL にアクセスするための認証ユーザー名とパスワード:");
                w.WriteLine("ユーザー名:");
                w.WriteLine(this.GeneratedUserName);
                w.WriteLine();

                w.WriteLine("パスワード:");
                w.WriteLine(this.GeneratedPassword);
                w.WriteLine();


            }

            if (this.GeneratedZipPassword._IsFilled())
            {
                w.WriteLine("■ ZIP ファイルの暗号化パスワード:");
                w.WriteLine(this.GeneratedZipPassword);
                w.WriteLine();
            }

            if (this.Expires < Util.MaxDateTimeOffsetValue)
            {
                w.WriteLine("■ ファイルのダウンロード有効期限:");
                w.WriteLine(this.Expires._ToLocalDtStr(option: DtStrOption.DateOnly));
                w.WriteLine();
            }

            if (this.AllowOnlyOnce)
            {
                w.WriteLine("■ セキュリティ機能:");
                w.WriteLine("ファイルは、最初にダウンロードした時から 60 分間は同じ端末 (IP)");
                w.WriteLine("から再度ダウンロード可能です。それ以降はダウンロードできません。");
                w.WriteLine();
            }

            if (this.GeneratedUserName._IsFilled())
            {
                w.WriteLine("■ 法律上の通知: 万一本メールを誤受信等で入手された場合");

                if (this.Recipient._IsFilled())
                {
                    w.WriteLine($"上記のユーザー名とパスワードは、アクセス制御機能の識別符号に該当し、\r\n「{this.Recipient.Trim()}」様専用に発行されたものです。");
                }
                else
                {
                    w.WriteLine(@"上記のユーザー名とパスワードは、アクセス制御機能の識別符号に該当し、
本メールの「本文」の冒頭に宛名人が指定されている場合は、
その宛名人のみに宛てて専用で発行されたものです。");
                }

                w.WriteLine(@"それ以外の方は、認証に使用することはできません。
万一、本メールがそれ以外の方に誤送信または共有された場合、
受信者が上記のユーザー名とパスワードを用いて URL にアクセスすることは
不正アクセス禁止法により禁止されています。本メールが誤受信である
と思われる場合、上記にアクセスをすることなく、送信者に誤送信の旨
をお知らせいただければ幸いです。誤受信された方が上記にアクセスを
してユーザー名とパスワードを入力する行為には、刑事責任および民事責任
が課せられます。");

                w.WriteLine("上記にかかわらず、本ファイルの送信者およびその同一組織の職員等が、");
                w.WriteLine("送信済みファイル内容および履歴の確認のため、自らアクセスすることは");
                w.WriteLine("許容されます。[アクセス管理者]");

                w.WriteLine();
            }

            w.WriteLine("--------- ファイルの送信のご案内 ここまで ----------");
            w.WriteLine();

            return w.ToString();
        }
    }

    public class UploadFormCookies
    {
        public bool Auth { get; set; } = true;
        public bool Log { get; set; } = true;
        public bool Zip { get; set; } = false;
        //public bool Once { get; set; } = false;
        //public int Days { get; set; } = 0;
    }

    public class UploadFormRequest
    {
        public string? Recipient { get; set; }
        public string? UrlHint { get; set; }
        public bool VeryShort { get; set; }
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
                fileName = PPWin.GetFileName(fileName)._MakeSafeFileName();

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
                foreach (var f in this.FileListInternal)
                {
                    await f._DisposeSafeAsync();
                }
            }
            finally
            {
                await base.CleanupImplAsync(ex);
            }
        }
    }

    public class UploadOption : INormalizable
    {
        public string? PIN { get; set; }
        public string? Destination { get; set; }
        public string? UrlHint { get; set; }
        public bool VeryShort { get; set; }
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
        readonly HiveData<AppSettings> HiveData;

        // データベースへのアクセスを容易にするための自動プロパティ
        public CriticalSection DbLock => HiveData.DataLock;
        public AppSettings Db => HiveData.ManagedData;
        public AppSettings DbSnapshot => HiveData.GetManagedDataSnapshot();

        public string RootDirectoryFullPath => PP.Combine(Env.AppRootDir, DbSnapshot.DataStoreRootDir);

        readonly LogBrowser Browser;

        public Server(CancellationToken cancel = default) : base(cancel)
        {
            try
            {
                // 多重起動を防止
                this.SingleInstance = new SingleInstance(FileCenterConsts.SingleInstanceKey);

                // 設定データベースの初期化
                this.HiveData = new HiveData<AppSettings>(Hive.SharedLocalConfigHive, FileCenterConsts.HiveSettingsName,
                    getDefaultDataFunc: () => new AppSettings(),
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

                RootDirectoryFullPath._Debug();

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
        public async Task<UploadResult> UploadAsync(DateTimeOffset timeStamp, string ipAddress, string baseUrl, UploadFileList fileList, UploadOption option, CancellationToken cancel = default)
        {
            option.Normalize();

            string newDirName;
            string newDirFullPath;
            string yymmddAndSeqNo;

            string authSubDirName = "";
            string firstFileRelativeName = "";

            Uri baseUri = baseUrl._ParseUrl();

            // PIN コードチェック
            string? currentPin = DbSnapshot.PIN;
            if (currentPin._IsFilled() && currentPin._IsSame(option.PIN) == false)
            {
                // PIN コード不正
                throw new CoresException("Incorrect PIN code.");
            }

            if (fileList.FileList.Count == 0)
            {
                // ファイルがない
                throw new CoresException($"You must specify at least one file to upload.");
            }

            if (fileList.FileList.Count > DbSnapshot.UploadNumLimit)
            {
                // ファイル数超過
                throw new CoresException($"Max uploadable files count is {DbSnapshot.UploadNumLimit}. You attempted to upload {fileList.FileList.Count} files.");
            }

            long totalStreamSize = 0;
            foreach (var file in fileList.FileList)
            {
                totalStreamSize += file.Stream.Length;
            }

            if (totalStreamSize > DbSnapshot.UploadSizeLimit)
            {
                // ファイルサイス超過
                throw new CoresException($"Max uploadable files total size is {DbSnapshot.UploadSizeLimit._GetFileSizeStr()}. You attempted to upload {totalStreamSize._GetFileSizeStr()}.");
            }

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
                    if (i >= 10000)
                    {
                        throw new CoresLibException("Too many retries: i >= 10000");
                    }

                    string yymmddAndSeqNoTmp = timeStamp.LocalDateTime.ToString("yyMMdd") + "_" + seqNo.ToString("D3");
                    string yymmdd = yymmddAndSeqNoTmp._SliceHead(6);

                    int sizeOfRandStr = 16;

                    // ユニークなルートディレクトリ名を決定する
                    if (option.UrlHint._IsFilled())
                    {
                        sizeOfRandStr = 16 - option.UrlHint.Length - 1;
                    }

                    sizeOfRandStr = Math.Max(sizeOfRandStr, 8);

                    string candidate = yymmdd + "/" + yymmddAndSeqNoTmp + "_";
                    if (option.UrlHint._IsFilled())
                    {
                        candidate += option.UrlHint + "_";
                    }

                    if (option.VeryShort == false)
                    {
                        candidate += Str.GenRandPassword(sizeOfRandStr, false).ToLower();
                    }
                    else
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            candidate += (char)((int)'0' + Secure.RandSInt31() % 10);
                        }
                    }

                    string fullPath = PP.Combine(this.RootDirectoryFullPath, candidate);

                    if (await Lfs.IsDirectoryExistsAsync(fullPath, cancel) ||
                        await Lfs.IsFileExistsAsync(fullPath, cancel))
                    {
                        // すでに存在する
                    }
                    else
                    {
                        // 決定 !!
                        newDirName = PP.GetFileName(candidate);
                        newDirFullPath = fullPath;
                        yymmddAndSeqNo = yymmddAndSeqNoTmp;
                        break;
                    }
                }
            }

            // パスワード等を生成
            UploadResult result = new UploadResult
            {
                Recipient = option.Destination._NonNull(),
                AllowOnlyOnce = option.Once,
            };

            if (option.Days <= 0)
            {
                result.Expires = Util.MaxDateTimeOffsetValue;
            }
            else
            {
                result.Expires = timeStamp.LocalDateTime.Date.AddDays(option.Days).AddDays(1).AddTicks(-1)._AsDateTimeOffset(true);
            }

            if (option.Auth)
            {
                result.GeneratedUserName = "u" + yymmddAndSeqNo._ReplaceStr("_", "");
                result.GeneratedPassword = "p" + Str.GenRandPassword(option.VeryShort ? 6 : 24, false);
                authSubDirName = "auth" + Str.GenRandNumericPassword(7);
            }

            if (option.Zip)
            {
                result.GeneratedZipPassword = "zip" + Str.GenRandPassword(option.VeryShort ? 6 : 32, false);
                result.IsZipped = true;
            }

            // ディレクトリ作成
            await Lfs.CreateDirectoryAsync(newDirFullPath, cancel: cancel);

            long totalSize = 0;

            string firstFileName = "";

            try
            {
                if (option.Zip == false)
                {
                    // 暗号化 ZIP なしの場合、アップロードされてきたファイルを生ファイルシステムにそのまま書いていく
                    foreach (var file in fileList.FileList)
                    {
                        if (firstFileName._IsEmpty()) firstFileName = PPWin.GetFileName(file.RelativeFileName);

                        string newFileFullPath = Lfs.PP.Combine(newDirFullPath, file.RelativeFileName);
                        if (option.Auth)
                        {
                            newFileFullPath += Consts.Extensions.EncryptedXtsAes256;
                        }

                        using var newFileObj = await Lfs.CreateAsync(newFileFullPath, flags: FileFlags.AutoCreateDirectory, cancel: cancel);
                        using Stream newFileStream = option.Auth == false ? (Stream)newFileObj.GetStream(true) : new XtsAesRandomAccess(newFileObj, result.GeneratedPassword!, true).GetStream(true);
                        totalSize += await file.Stream.CopyBetweenStreamAsync(newFileStream, cancel: cancel, flush: true);

                        if (firstFileRelativeName._IsEmpty())
                            firstFileRelativeName = file.RelativeFileName;
                    }
                }
                else
                {
                    // 暗号化 ZIP ありの場合、すべてのファイルを ZIP で圧縮・暗号化した上で 1 つの ZIP ファイルとして保存する
                    string zipFileName = yymmddAndSeqNo + "_" + PPWin.GetFileName(fileList.FileList[0].RelativeFileName) + Consts.Extensions.Zip;
                    string zipFileFullPath = Lfs.PP.Combine(newDirFullPath, zipFileName);
                    if (option.Auth)
                    {
                        zipFileFullPath += Consts.Extensions.EncryptedXtsAes256;
                    }

                    using var zipOutputFile = await Lfs.CreateAsync(zipFileFullPath, flags: FileFlags.AutoCreateDirectory, cancel: cancel);
                    using IRandomAccess<byte> zipOutputFileRandomAccess = option.Auth == false ? (IRandomAccess<byte>)zipOutputFile : new XtsAesRandomAccess(zipOutputFile, result.GeneratedPassword!, true);
                    using var zipWriter = new ZipWriter(new ZipContainerOptions(zipOutputFileRandomAccess));

                    foreach (var file in fileList.FileList)
                    {
                        if (firstFileName._IsEmpty()) firstFileName = Lfs.PP.GetFileName(file.RelativeFileName);

                        var metadata = new FileMetadata(attributes: FileAttributes.Normal, creationTime: timeStamp, lastWriteTime: timeStamp, lastAccessTime: timeStamp);

                        totalSize += await zipWriter.ImportVirtualFileAsync(file.Stream,
                            new FileContainerEntityParam(file.RelativeFileName, metadata,
                                FileContainerEntityFlags.EnableCompression | FileContainerEntityFlags.CompressionMode_Fast,
                                encryptPassword: result.GeneratedZipPassword,
                                encoding: file.RelativeFileName._GetBestSuitableEncoding()
                            ), cancel);
                    }

                    await zipWriter.FinishAsync(cancel);

                    firstFileRelativeName = zipFileName;
                }

                result.NumFiles = fileList.FileList.Count;
                result.TotalFileSize = totalSize;

                result.FirstFileNameForPrint = firstFileName;

                if (totalSize > DbSnapshot.UploadSizeLimit)
                {
                    // ファイルサイス超過
                    throw new CoresException($"Max uploadable files total size is {DbSnapshot.UploadSizeLimit._GetFileSizeStr()}. You attempted to upload {totalStreamSize._GetFileSizeStr()}.");
                }

                // _secure.json ファイルを出力する
                LogBrowserSecureJson secureJson = new LogBrowserSecureJson
                {
                    AuthRequired = option.Auth,
                    AuthSubject = option.Destination._NonNull(),
                    Expires = result.Expires,
                    DisableAccessLog = false,
                    AllowAccessToAccessLog = option.LogAccess,
                    UploadTimeStamp = timeStamp,
                    UploadIp = ipAddress,
                    AllowOnlyOnce = option.Once,
                    TotalFileSize = result.TotalFileSize,
                    NumFiles = result.NumFiles,
                };

                if (option.Auth)
                {
                    secureJson.AuthDatabase = new KeyValueList<string, string>();
                    secureJson.AuthDatabase.Add(result.GeneratedUserName!, Secure.SaltPassword(result.GeneratedPassword!));
                    secureJson.AuthSubDirName = authSubDirName;
                }

                // URL 生成
                result.GeneratedUrlDir = baseUri._CombineUrl(FileCenterConsts.FileBrowserHttpDir + "/" + newDirName + "/").ToString();
                result.GeneratedUrlDirAuthDirect = baseUri._CombineUrl(FileCenterConsts.FileBrowserHttpDir + "/" + newDirName + "/" + (option.Auth ? authSubDirName + "/" : "")).ToString();
                result.GeneratedUrlFirstFileDirect = result.GeneratedUrlDirAuthDirect._CombineUrl(firstFileRelativeName).ToString();

                if (option.Auth == false)
                {
                    result.GeneratedUrlFirstFileAuthCredentialDirect = result.GeneratedUrlFirstFileDirect;
                    result.GeneratedUrlDirAuthCredentialDirect = result.GeneratedUrlDirAuthDirect;
                }
                else
                {
                    string tmp = result.GeneratedUrlFirstFileDirect;
                    if (tmp.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        tmp = "http://" + result.GeneratedUserName + ":" + result.GeneratedPassword + "@" + tmp._Slice(7);
                    }
                    else if (tmp.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        tmp = "https://" + result.GeneratedUserName + ":" + result.GeneratedPassword + "@" + tmp._Slice(8);
                    }

                    result.GeneratedUrlFirstFileAuthCredentialDirect = tmp;



                    tmp = result.GeneratedUrlDirAuthDirect;
                    if (tmp.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        tmp = "http://" + result.GeneratedUserName + ":" + result.GeneratedPassword + "@" + tmp._Slice(7);
                    }
                    else if (tmp.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        tmp = "https://" + result.GeneratedUserName + ":" + result.GeneratedPassword + "@" + tmp._Slice(8);
                    }

                    result.GeneratedUrlDirAuthCredentialDirect = tmp;
                }

                secureJson.Normalize();

                await Lfs.WriteJsonToFileAsync(Lfs.PP.Combine(newDirFullPath, Consts.FileNames.LogBrowserSecureJson), secureJson, cancel: cancel);

                return result;
            }
            catch
            {
                // 何らかの処理に失敗した場合はディレクトリごと削除する
                try
                {
                    await Lfs.DeleteDirectoryAsync(newDirFullPath, true);
                }
                catch (Exception ex)
                {
                    ex._Error();
                }
                throw;
            }
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


