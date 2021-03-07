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

        public const string FileBrowserDownloadHttpDir = "/d";

        public const string FileBrowserUploadInboxHttpDir = "/u";

        public const string DefaultWebSiteTitle = "「IPA ファイルの窓口」 - オウプンソース セキュアファイルアップロードシステム";

        public const int MaxUploadFileFormElements = 10;

        public const int MaxUrlHintLen = 32;

        // 最大アップロード可能容量
        public const long UploadSizeHardLimit = 100L * 1024 * 1024 * 1024; // 100GB

        // デフォルトアップロード可能容量
        public const long DefaultUploadSizeLimit = 2L * 1024 * 1024 * 1024; // 2GB

        // デフォルトアップロード可能ファイル数
        public const int DefaultUploadNumLimit = 100;
    }

    public class InboxUploadLog
    {
        public DateTimeOffset Timestamp;
        public string? IP;
        public int Port;
        public string? Url;
        public string? UserAgent;
        public string? Referer;
        public string? UploadedFileName;
        public long FileSize;
    }

    public class AppSettings : INormalizable, IValidatableObject, IValidatable
    {
        [Display(Name = "Web サイト表示名")]
        [Required]
        public string WebSiteTitle { get; set; } = "";

        [Display(Name = "システム利用パスワード")]
        public string? PIN { get; set; }

        [Display(Name = "一度にアップロード可能なファイルの合計サイズ")]
        public long UploadSizeLimit { get; set; }

        [Display(Name = "一度にアップロード可能なファイルの数")]
        public int UploadNumLimit { get; set; }

        [Display(Name = "SMTP サーバーホスト名")]
        public string? SmtpHostname { get; set; }

        [Display(Name = "SMTP サーバーポート番号")]
        public int SmtpPort { get; set; } = 0;

        [Display(Name = "SMTP 送信元メールアドレス")]
        public string? SmtpFrom { get; set; }

        [Display(Name = "SMTP で SSL/TLS を利用 (1: はい, 0: いいえ)")]
        public int SmtpUseSsl { get; set; } = 0;

        [Display(Name = "SMTP サーバー認証ユーザー名")]
        public string? SmtpUsername { get; set; }

        [Display(Name = "SMTP サーバー認証パスワード")]
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
        public string? GeneratedInboxUploadPassword { get; set; }
        public bool IsZipped { get; set; }
        public string? GeneratedZipPassword { get; set; }
        public string Recipient { get; set; } = "";
        public DateTimeOffset Expires { get; set; } = Util.MaxDateTimeOffsetValue;
        public int NumFiles { get; set; }
        public long TotalFileSize { get; set; }
        public bool AllowOnlyOnce { get; set; }
        public string FirstFileNameForPrint { get; set; } = "";
        public bool IsCreatingUploadInbox { get; set; }
        public bool IsUploadingForInbox { get; set; }
        public string? EmailSentForInbox { get; set; }
        public string GeneratedUrlUploadDir { get; set; } = "";
        public string InboxIpAcl { get; set; } = "";
        public string DeleteCode { get; set; } = "";

        public override string ToString() => ToString(false);

        public string ToString(bool forInboxUploader)
        {
            StringWriter w = new StringWriter();

            if (forInboxUploader && this.IsCreatingUploadInbox == false)
            {
                throw new ArgumentException(nameof(forInboxUploader));
            }

            if (forInboxUploader == false)
            {
                if (IsUploadingForInbox)
                {
                    w.WriteLine("--------- ファイルのアップロード完了の通知 ここから ----------");
                }
                else if (IsCreatingUploadInbox == false)
                {
                    w.WriteLine("--------- ファイルの送付のご案内 ここから ----------");
                }
                else
                {
                    w.WriteLine("--------- ゲストアップロード領域へのアクセス方法 (オーナー向け) ここから ----------");
                }

                if (IsCreatingUploadInbox == false)
                {
                    if (this.Recipient._IsFilled())
                    {
                        w.WriteLine($"{this.Recipient.Trim()} 様");
                        w.WriteLine();
                    }

                    string tmp = "お送りいたします。";
                    if (IsUploadingForInbox)
                    {
                        tmp = "アップロードしましたので、お知らせします。";
                    }

                    if (this.NumFiles == 1)
                    {
                        w.WriteLine($"ファイル 「{this.FirstFileNameForPrint}」 ({this.TotalFileSize._GetFileSizeStr()}) を{tmp}");
                    }
                    else
                    {
                        w.WriteLine($"ファイル 「{this.FirstFileNameForPrint}」 等 {this.NumFiles._ToString3()} ファイル (合計 {this.TotalFileSize._GetFileSizeStr()}) を{tmp}");
                    }

                    if (IsUploadingForInbox)
                    {
                        w.WriteLine("ファイルは、以下のゲストアップロード領域にアップロードされました。");
                    }
                    else
                    {
                        w.WriteLine("大変お手数ですが、以下の URL にアクセスの上、ダウンロードをお願いいたします。");
                    }
                }
                else
                {
                    w.WriteLine("このたび作成されたゲストアップロード領域へのアクセス方法は、以下のとおりである。");
                }

                w.WriteLine();

                if (IsUploadingForInbox)
                {
                    w.WriteLine("■ ゲストアップロード領域の URL:");
                }
                else
                {
                    w.WriteLine("■ ファイルをダウンロードするための URL:");
                }
                w.WriteLine(GeneratedUrlDir);

                if (this.GeneratedUserName._IsFilled())
                {
                    w.WriteLine($"ユーザー名: {this.GeneratedUserName}");
                    w.WriteLine($"パスワード: {this.GeneratedPassword}");
                    w.WriteLine("※ この URL は第三者に配布・転載しないでください。");
                    w.WriteLine("※ 上記のユーザー・パスワードは、アクセス制御の識別符号に該当します。");
                    w.WriteLine("   本メッセージの宛名人に発行されたものであり、他人は使用できません。");
                    w.WriteLine("   詳しくは、下記の「法律上の注意」をお読みください。");
                    w.WriteLine();
                }

                if (this.GeneratedZipPassword._IsFilled())
                {
                    w.WriteLine($"■ ZIP ファイルの暗号化パスワード: {this.GeneratedZipPassword}");
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
                    w.WriteLine("■ ファイルをダウンロードするための URL");
                    w.WriteLine("  (ユーザー名とパスワードが埋め込まれた 1 行 URL):");
                    w.WriteLine(this.GeneratedUrlDirAuthCredentialDirect);
                    w.WriteLine("- 上記 URL がメールソフトウェアの都合で改行されている場合は、");
                    w.WriteLine("  お手数ですが 1 行に結合していただいた上でアクセスをお願いします。");
                    w.WriteLine("- Internet Explorer では上記 URL は使用できません。");
                    w.WriteLine("  Chrome, Firefox, Edge 等のブラウザをご利用ください。");
                    w.WriteLine("- Chrome のバージョンによっては、パスワードが埋め込まれた URL が");
                    w.WriteLine("  正しく動作しません。その場合は、上述のユーザー名とパスワード部分");
                    w.WriteLine("  の手動入力をお願いいたします。");
                    w.WriteLine("※ この URL は第三者に配布・転載しないでください。");
                    w.WriteLine("※ 上記の URL にはユーザー名とパスワードが埋め込まれており、");
                    w.WriteLine("   アクセス制御機能の識別符号に該当します。");
                    w.WriteLine("   本メッセージの宛名人に発行されたものであり、他人は使用できません。");
                    w.WriteLine("   詳しくは、下記の「法律上の注意」をお読みください。");
                    w.WriteLine();

                    if (this.GeneratedZipPassword._IsFilled())
                    {
                        w.WriteLine($"■ ZIP ファイルの暗号化パスワード: {this.GeneratedZipPassword}");
                        w.WriteLine();
                    }
                }

                if (IsCreatingUploadInbox == false)
                {
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
                }

                if (IsUploadingForInbox)
                {
                    w.WriteLine("--------- ファイルのアップロード完了の通知 ここから ----------");
                }
                else if (IsCreatingUploadInbox == false)
                {
                    w.WriteLine("--------- ファイルの送付のご案内 ここまで ----------");
                }
                else
                {
                    w.WriteLine("--------- このゲストアップロード領域へのアクセス方法 (オーナー向け) ここまで ----------");
                }

                w.WriteLine();
            }
            else
            {
                w.WriteLine("--------- ファイルのアップロード依頼 ここから ----------");

                w.WriteLine("ファイルをアップロードいただくための URL をお送りいたします。");
                w.WriteLine("大変お手数ですが、以下の URL にアクセスの上、アップロードをお願いいたします。");

                w.WriteLine();

                w.WriteLine("■ ファイルをアップロードするための URL:");
                w.WriteLine(GeneratedUrlUploadDir);

                w.WriteLine();
                w.WriteLine("上記 URL にアクセスいただきますと、ファイルをアップロードするためのフォームが表示されます。");
                w.WriteLine("画面の指示に従い、ファイルのアップロードをお願いいたします。");

                w.WriteLine();
                w.WriteLine("※ この URL は第三者に配布・転載しないでください。");
                w.WriteLine();

                w.WriteLine("--------- ファイルの送信のご案内 ここまで ----------");
                w.WriteLine();
            }

            return w.ToString();
        }
    }

    public class DeleteForm
    {
        public string? Url { get; set; }
        public string? Code { get; set; }
        public bool Force { get; set; }
    }

    public class UploadFormCookies
    {
        public bool Auth { get; set; } = true;
        public bool Log { get; set; } = true;
        public bool Zip { get; set; } = false;
        //public bool Once { get; set; } = false;
        //public int Days { get; set; } = 0;
        public string Email { get; set; } = "";
        public bool InboxForcePrefixYymmdd { get; set; } = false;
        public bool VeryShort { get; set; } = false;
        public string InboxIpAcl { get; set; } = "";
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

        public string? Email { get; set; }
        public bool InboxForcePrefixYymmdd { get; set; }
        public string? InboxIpAcl { get; set; }

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
        public bool IsInboxCreateMode { get; set; }
        public bool IsInboxUploadMode { get; set; }
        public bool InboxForcePrefixYymmdd { get; set; }
        public string InboxId { get; set; } = "";
        public string InboxUploadPassword { get; set; } = "";
        public string InboxIpAcl { get; set; } = "";

        public string? PIN { get; set; }
        public string? Destination { get; set; }
        public string? UrlHint { get; set; }
        public bool VeryShort { get; set; }
        public bool Auth { get; set; }
        public bool LogAccess { get; set; }
        public bool Zip { get; set; }
        public bool Once { get; set; }
        public int Days { get; set; }

        public string? Email { get; set; }

        public string IpAddress { get; set; } = "";

        public void Normalize()
        {
            this.Destination = this.Destination._NonNullTrimSe()._TruncStr(Consts.MaxLens.NormalStringTruncateLen);
            this.UrlHint = this.UrlHint._Normalize(true, true, false, false);

            this.UrlHint = this.UrlHint.ToLower();

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

            this.Email = this.Email._NonNullTrim();
            this.InboxId = this.InboxId._NonNullTrim();
            this.InboxUploadPassword = this.InboxUploadPassword._NonNullTrim();
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

        readonly StatMan Stat;

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

                this.Stat = new StatMan(new StatManConfig { SystemName = "filecenter", LogName = "filecenter_stat" });

                Browser = new LogBrowser(new LogBrowserOptions(this.RootDirectoryFullPath,
                    systemTitle: this.DbSnapshot.WebSiteTitle,
                    stat: this.Stat,
                    flags: LogBrowserFlags.NoPreview | LogBrowserFlags.NoRootDirectory | LogBrowserFlags.SecureJson), FileCenterConsts.FileBrowserDownloadHttpDir);

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
        readonly NamedAsyncLocks NamedDirectoryLocks = new NamedAsyncLocks(StrComparer.IgnoreCaseTrimComparer);

        // URL 削除処理
        public async Task DeleteAsync(DateTimeOffset timeStamp, string clientIpAddress, string url, string code, bool force, CancellationToken cancel = default)
        {
            // URL のパース
            var uri = url._ParseUrl();
            string[] tokens = uri.AbsolutePath._Split(StringSplitOptions.RemoveEmptyEntries, '/');

            string? id = null;

            foreach (string token in tokens)
            {
                if (token.Length >= 8 && token._SliceHead(6)._IsNumber() && token[6] == '_')
                {
                    id = token;
                }
            }

            if (id._IsEmpty()) throw new CoresException("URL が不正です。");

            id = PathParser.Windows.MakeSafeFileName(id.Trim());
            string dirFullPath = PP.Combine(this.RootDirectoryFullPath, id.Substring(0, 6), id);
            string secureJsonFullPath = PP.Combine(dirFullPath, Consts.FileNames.LogBrowserSecureJson);
            if (await Lfs.IsDirectoryExistsAsync(dirFullPath, cancel) == false ||
                await Lfs.IsFileExistsAsync(secureJsonFullPath, cancel) == false)
            {
                throw new CoresException("Invalid Uploader URL. 指定された URL は存在しません。改行が自動的に挿入されてしまっている場合は、改行をまたいで URL を連結し、もう一度アクセスしてみてください。(1)");
            }

            // _secure.json を読み込んでみる
            var data = await Lfs.ReadJsonFromFileAsync<LogBrowserSecureJson>(secureJsonFullPath, cancel: cancel);

            // コードの確認
            if (code._IsEmpty() && force)
            {
                // 強制モード
            }
            else
            {
                // 通常モード コード検査
                if (data.DeleteCode._IsEmpty())
                {
                    throw new CoresException("Delete code is not specified. 緊急削除コードが指定されていません。");
                }

                if (data.DeleteCode._IsSamei(code) == false)
                {
                    throw new CoresException("Invalid delete code. 緊急削除コードが正しくありません。");
                }
            }

            // すでに削除されているか?
            if (data.IsDeleted)
            {
                throw new CoresException($"Already deleted. 指定された URL は、すでに削除されています。削除日時: {data.DeletedTimeStamp._ToFullDateTimeStr()}, 削除要求元 IP アドレス: {data.DeleteIp}");
            }

            // 削除処理
            data.IsDeleted = true;
            data.DeletedTimeStamp = timeStamp;
            data.DeleteIp = clientIpAddress;

            // 結果を保存
            await Lfs.WriteJsonToFileAsync(secureJsonFullPath, data, cancel: cancel);

            Stat.AddReport("DeletedUrlsCount_Total", 1);
        }

        // アップロードメイン処理
        public async Task<UploadResult> UploadAsync(DateTimeOffset timeStamp, string clientIpAddress, int clientPort, string baseUrl, UploadFileList fileList, UploadOption option, CancellationToken cancel = default)
        {
            option.Normalize();

            StringWriter emailBody = new StringWriter();

            string newDirName;
            string newDirFullPath;
            string yymmddAndSeqNo;

            string forcedPrefixDirName = "";

            string authSubDirName = "";
            string firstFileRelativeName = "";

            string hostNameOrIp = await LocalNet.GetHostNameSingleOrIpAsync(clientIpAddress, cancel);

            LogBrowserSecureJson? existingSecureJson = null;

            Uri baseUri = baseUrl._ParseUrl();

            // PIN コードチェック
            if (option.IsInboxUploadMode == false)
            {
                string? currentPin = DbSnapshot.PIN;
                if (currentPin._IsFilled() && currentPin._IsSame(option.PIN) == false)
                {
                    // PIN コード不正
                    throw new CoresException("Incorrect PIN code.");
                }
            }

            if (option.IsInboxCreateMode == false)
            {
                if (fileList.FileList.Count == 0)
                {
                    // ファイルがない
                    throw new CoresException($"You must specify at least one file to upload.");
                }
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

                string fn = PPWin.GetFileName(file.RelativeFileName);

                foreach (string token in PPWin.SplitTokens(file.RelativeFileName))
                {
                    if (Consts.FileNames.IsSpecialFileNameForLogBrowser(token))
                    {
                        throw new CoresException($"Incorrect directory or filename.");
                    }
                }
            }

            if (totalStreamSize > DbSnapshot.UploadSizeLimit)
            {
                // ファイルサイス超過
                throw new CoresException($"Max uploadable files total size is {DbSnapshot.UploadSizeLimit._GetFileSizeStr()}. You attempted to upload {totalStreamSize._GetFileSizeStr()}.");
            }

            string emailSubject = $"[通知] ゲストアップロード領域 /{option.InboxId}/ に {fileList.FileList.Count} 個のファイル ({totalStreamSize._GetFileSizeStr()}) がアップロードされました";

            var dbSnap = DbSnapshot;

            emailBody.WriteLine($"{dbSnap.WebSiteTitle}\r\n{baseUri._CombineUrl("/")} からのご連絡");

            emailBody.WriteLine();

            emailBody.WriteLine($"ゲストアップロード領域 /{option.InboxId}/ にファイルがアップロードされましたので通知いたします。");

            emailBody.WriteLine();

            emailBody.WriteLine($"アップロードされたファイル (合計 {fileList.FileList.Count} ファイル、{totalStreamSize._GetFileSizeStr()}):");

            fileList.FileList._DoForEach(x => emailBody.WriteLine($"- {PPMac.NormalizeRelativePath(x.RelativeFileName)} ({x.Stream.Length._GetFileSizeStr()})"));

            emailBody.WriteLine();
            emailBody.WriteLine("アップロード日時:");
            emailBody.WriteLine(timeStamp._ToFullDateTimeStr());
            emailBody.WriteLine();
            emailBody.WriteLine("アップロード元 IP アドレス: " + (hostNameOrIp._IsSamei(clientIpAddress) ? clientIpAddress : $"{hostNameOrIp} ({clientIpAddress})"));
            emailBody.WriteLine();
            emailBody.WriteLine("これらのファイルをダウンロードするには、以下の URL にアクセスしてください。");

            string tmpUrl = baseUri._CombineUrl(FileCenterConsts.FileBrowserDownloadHttpDir + "/" + option.InboxId + "/").ToString();
            emailBody.WriteLine(tmpUrl);
            emailBody.WriteLine();
            emailBody.WriteLine();

            emailBody.WriteLine($"{dbSnap.WebSiteTitle}\r\n{baseUri._CombineUrl("/")}");
            emailBody.WriteLine();

            if (option.IsInboxUploadMode == false)
            {
                // 通常モード または受信トレイ作成モードの場合は、新しいディレクトリ名を決定する
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
            }
            else
            {
                // 受信トレイへのファイルアップロード要求の場合は、指定された受信トレイ ID をもとに物理ディレクトリにアクセスする
                string safeInboxId = PathParser.Windows.MakeSafeFileName(option.InboxId.Trim());
                string testDirFullPath = PP.Combine(this.RootDirectoryFullPath, safeInboxId.Substring(0, 6), safeInboxId);
                string testSecureJsonFullPath = PP.Combine(testDirFullPath, Consts.FileNames.LogBrowserSecureJson);

                if (await Lfs.IsDirectoryExistsAsync(testDirFullPath, cancel) == false ||
                    await Lfs.IsFileExistsAsync(testSecureJsonFullPath, cancel) == false)
                {
                    throw new CoresException("Invalid Uploader URL. 指定されたアップロード用 URL が不正です。電子メール等で URL を受け取った場合は、URL に自動的に改行等が入っていないかどうかご確認ください。改行が自動的に挿入されてしまっている場合は、改行をまたいで URL を連結し、もう一度アクセスしてみてください。(1)");
                }

                // _secure.json を読み込んでみる
                existingSecureJson = await Lfs.ReadJsonFromFileAsync<LogBrowserSecureJson>(testSecureJsonFullPath, cancel: cancel);

                if (existingSecureJson.IsInbox == false)
                {
                    throw new CoresException("Security error !! The specified URL is not an inbox.");
                }

                if (existingSecureJson.InboxUploadPassword._IsEmpty() || existingSecureJson.InboxUploadPassword != option.InboxUploadPassword)
                {
                    throw new CoresException("Invalid Uploader URL. 指定されたアップロード用 URL が不正です。電子メール等で URL を受け取った場合は、URL に自動的に改行等が入っていないかどうかご確認ください。改行が自動的に挿入されてしまっている場合は、改行をまたいで URL を連結し、もう一度アクセスしてみてください。(2)");
                }

                if (EasyIpAcl.Evaluate(existingSecureJson.InboxIpAcl, clientIpAddress) != EasyIpAclAction.Permit)
                {
                    throw new CoresException($"Invalid Upload Source IP Address. 指定されたアップロード用 URL は、特定の IP アドレスからのみアップロードすることが許可されています。あなたの端末の IP アドレス '{clientIpAddress}' からは、アップロードすることはできません。");
                }

                yymmddAndSeqNo = "";

                newDirFullPath = testDirFullPath;
                newDirName = PP.GetFileName(testDirFullPath);

                if (existingSecureJson.InboxForcePrefixYymmdd)
                {
                    // prefix ディレクトリ名を強制的に付けるオプションが有効である
                    string prefixYymmdd = timeStamp.LocalDateTime.ToString("yyyyMMdd_HHmmss");

                    string tmp;

                    if (hostNameOrIp._IsSamei(clientIpAddress))
                    {
                        tmp = $"{prefixYymmdd}_" + clientIpAddress;
                    }
                    else
                    {
                        tmp = $"{prefixYymmdd}_" + clientIpAddress + "_" + hostNameOrIp;
                    }

                    forcedPrefixDirName = PP.MakeSafeFileName(tmp);
                }
            }

            // パスワード等を生成
            UploadResult result = new UploadResult
            {
                Recipient = option.Destination._NonNull(),
                AllowOnlyOnce = option.Once,
            };

            if (option.IsInboxUploadMode == false)
            {
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
                    result.GeneratedUserName = "user" + yymmddAndSeqNo._ReplaceStr("_", "");
                    result.GeneratedPassword = "pass" + Str.GenRandPassword((option.VeryShort && option.IsInboxCreateMode == false) ? 6 : 24, false);
                    authSubDirName = "auth" + Str.GenRandNumericPassword(7);
                }

                if (option.Zip)
                {
                    result.GeneratedZipPassword = "zip" + Str.GenRandPassword(option.VeryShort ? 6 : 32, false);
                    result.IsZipped = true;
                }
            }

            if (option.IsInboxCreateMode)
            {
                result.GeneratedInboxUploadPassword = (option.VeryShort ? Str.GenRandNumericPasswordWithBlocks(4, 3) : Str.GenRandPassword(24, false)).ToLower();
                result.IsCreatingUploadInbox = true;
            }

            // ディレクトリ作成
            if (option.IsInboxUploadMode == false)
            {
                await Lfs.CreateDirectoryAsync(newDirFullPath, cancel: cancel);

                await Lfs.SetDirectoryMetadataAsync(newDirFullPath, new FileMetadata(timeStamp), cancel);
            }

            long totalSize = 0;

            string firstFileName = "";

            try
            {
                using var dirLock = await NamedDirectoryLocks.LockWithAwait(newDirFullPath, cancel); // ユニークなディレクトリ名単位でロックする (受信トレイアップロードモードで競合読み書きが発生しないように)

                if (option.Zip == false || option.IsInboxUploadMode)
                {
                    // 暗号化 ZIP なしの場合、アップロードされてきたファイルを生ファイルシステムにそのまま書いていく
                    foreach (var file in fileList.FileList)
                    {
                        string relativeFileName = file.RelativeFileName;

                        if (forcedPrefixDirName._IsFilled())
                        {
                            relativeFileName = PP.Combine(forcedPrefixDirName, relativeFileName);
                        }

                        if (firstFileName._IsEmpty()) firstFileName = PPWin.GetFileName(relativeFileName);

                        string newFileFullPath = Lfs.PP.Combine(newDirFullPath, relativeFileName);
                        if (option.Auth)
                        {
                            newFileFullPath += Consts.Extensions.EncryptedXtsAes256;
                        }

                        if (option.IsInboxUploadMode)
                        {
                            if (await Lfs.IsFileExistsAsync(newFileFullPath, cancel))
                            {
                                // 受信トレイアップロードモードの場合、アップロード先ファイルがすでに存在する場合は履歴フォルダを作成して移動する
                                var existingFileMetadata = await Lfs.GetFileMetadataAsync(newFileFullPath, cancel: cancel);

                                // 新しい履歴ファイルパスの作成
                                // まず、既存ファイルの更新日時を取得
                                DateTimeOffset existingTimeStamp = existingFileMetadata.LastWriteTime!.Value;

                                // ふさわしいディレクトリ名を決定
                                string existingYymmdd = existingTimeStamp.LocalDateTime.ToString("yyyyMMdd_HHmmss");

                                string subDirName = PP.Combine(Consts.FileNames.LogBrowserHistoryDirName, existingYymmdd);

                                string historyDirBaseName = PP.Combine(newDirFullPath, subDirName);
                                string historyFileFullName = PP.Combine(historyDirBaseName, file.RelativeFileName);
                                string historyDirFullName = PP.GetDirectoryName(historyFileFullName);

                                await Lfs.CreateDirectoryAsync(historyDirFullName, cancel: cancel);

                                if (await Lfs.IsFileExistsAsync(historyFileFullName, cancel))
                                {
                                    await Lfs.DeleteFileAsync(historyFileFullName);
                                }
                                await Lfs.MoveFileAsync(newFileFullPath, historyFileFullName, cancel);

                                await Lfs.SetFileMetadataAsync(historyFileFullName, new FileMetadata(existingTimeStamp), cancel);

                                await Lfs.SetDirectoryMetadataAsync(historyDirFullName, new FileMetadata(existingTimeStamp), cancel);
                                await Lfs.SetDirectoryMetadataAsync(historyDirBaseName, new FileMetadata(existingTimeStamp), cancel);
                                await Lfs.SetDirectoryMetadataAsync(PP.GetDirectoryName(historyDirBaseName), new FileMetadata(timeStamp), cancel);
                            }
                        }

                        long thisFileSize = 0;

                        using (var newFileObj = await Lfs.CreateAsync(newFileFullPath, flags: FileFlags.AutoCreateDirectory, cancel: cancel))
                        {
                            using Stream newFileStream = option.Auth == false ? (Stream)newFileObj.GetStream(true) : new XtsAesRandomAccess(newFileObj, result.GeneratedPassword!, true).GetStream(true);
                            long sz = await file.Stream.CopyBetweenStreamAsync(newFileStream, cancel: cancel, flush: true);

                            thisFileSize += sz;
                            totalSize += sz;

                            if (firstFileRelativeName._IsEmpty())
                                firstFileRelativeName = file.RelativeFileName;
                        }

                        await Lfs.SetFileMetadataAsync(newFileFullPath, new FileMetadata(timeStamp), cancel);

                        await Lfs.SetDirectoryMetadataAsync(PP.GetDirectoryName(newFileFullPath), new FileMetadata(timeStamp), cancel);

                        if (option.IsInboxUploadMode)
                        {
                            InboxUploadLog log = new InboxUploadLog
                            {
                                Timestamp = timeStamp,
                                IP = clientIpAddress,
                                Port = clientPort,
                                Url = baseUrl,
                                UploadedFileName = PPLinux.NormalizeDirectorySeparatorIncludeWindowsBackslash(relativeFileName),
                                FileSize = thisFileSize,
                            };

                            await Browser.WriteAccessLogAsync("/" + PathParser.Windows.MakeSafeFileName(option.InboxId.Trim()) + "/" + Consts.FileNames.LogBrowserAccessLogDirName + "/", log, timeStamp, cancel);
                        }
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

                    using (var zipOutputFile = await Lfs.CreateAsync(zipFileFullPath, flags: FileFlags.AutoCreateDirectory, cancel: cancel))
                    {
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
                    }

                    await Lfs.SetFileMetadataAsync(zipFileFullPath, new FileMetadata(timeStamp), cancel);

                    await Lfs.SetDirectoryMetadataAsync(PP.GetDirectoryName(zipFileFullPath), new FileMetadata(timeStamp), cancel);

                    firstFileRelativeName = zipFileName;
                }

                result.NumFiles = fileList.FileList.Count;
                result.TotalFileSize = totalSize;

                result.FirstFileNameForPrint = firstFileName;

                if (option.IsInboxUploadMode == false)
                {
                    if (totalSize > DbSnapshot.UploadSizeLimit)
                    {
                        // ファイルサイス超過
                        throw new CoresException($"Max uploadable files total size is {DbSnapshot.UploadSizeLimit._GetFileSizeStr()}. You attempted to upload {totalStreamSize._GetFileSizeStr()}.");
                    }
                }

                if (option.IsInboxUploadMode == false)
                {
                    // _secure.json ファイルを出力する
                    LogBrowserSecureJson secureJson = new LogBrowserSecureJson
                    {
                        AuthRequired = option.Auth,
                        AuthSubject = option.Destination._NonNull(),
                        Expires = result.Expires,
                        DisableAccessLog = false,
                        AllowAccessToAccessLog = option.LogAccess,
                        UploadTimeStamp = timeStamp,
                        UploadIp = clientIpAddress,
                        AllowOnlyOnce = option.Once,
                        TotalFileSize = result.TotalFileSize,
                        NumFiles = result.NumFiles,
                        InboxEmail = option.Email._NonNull(),
                        InboxIpAcl = EasyIpAcl.NormalizeRules(option.InboxIpAcl),
                        AllowZipDownload = option.IsInboxCreateMode || (option.Auth == false && option.Zip == false),
                        DeleteCode = Str.GenRandNumericPasswordWithBlocks(4, 3),
                    };

                    result.DeleteCode = secureJson.DeleteCode;

                    result.InboxIpAcl = secureJson.InboxIpAcl;

                    if (option.IsInboxCreateMode)
                    {
                        secureJson.IsInbox = true;
                        secureJson.InboxUploadPassword = result.GeneratedInboxUploadPassword!;
                        secureJson.InboxForcePrefixYymmdd = option.InboxForcePrefixYymmdd;
                    }

                    if (option.Auth)
                    {
                        secureJson.AuthDatabase = new KeyValueList<string, string>();
                        secureJson.AuthDatabase.Add(result.GeneratedUserName!, Secure.SaltPassword(result.GeneratedPassword!));
                        secureJson.AuthSubDirName = authSubDirName;
                    }

                    secureJson.Normalize();

                    await Lfs.WriteJsonToFileAsync(Lfs.PP.Combine(newDirFullPath, Consts.FileNames.LogBrowserSecureJson), secureJson, cancel: cancel);
                }

                // URL 生成
                result.GeneratedUrlDir = baseUri._CombineUrl(FileCenterConsts.FileBrowserDownloadHttpDir + "/" + newDirName + "/").ToString();

                if (option.IsInboxUploadMode == false)
                {
                    result.GeneratedUrlDirAuthDirect = baseUri._CombineUrl(FileCenterConsts.FileBrowserDownloadHttpDir + "/" + newDirName + "/" + (option.Auth ? authSubDirName + "/" : "")).ToString();
                }
                else
                {
                    result.GeneratedUrlDirAuthDirect = baseUri._CombineUrl(FileCenterConsts.FileBrowserDownloadHttpDir + "/" + newDirName + "/" + (existingSecureJson!.AuthRequired ? existingSecureJson.AuthSubDirName + "/" : "")).ToString();
                }

                result.GeneratedUrlFirstFileDirect = result.GeneratedUrlDirAuthDirect._CombineUrl(firstFileRelativeName).ToString();

                if (option.IsInboxCreateMode)
                {
                    result.GeneratedUrlUploadDir = baseUri._CombineUrl(FileCenterConsts.FileBrowserUploadInboxHttpDir + "/" + newDirName + "/" + result.GeneratedInboxUploadPassword + "/").ToString();
                }

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

                // ディレクトリの更新日時の変更
                if (option.IsInboxUploadMode == false)
                {
                    await Lfs.SetDirectoryMetadataAsync(newDirFullPath, new FileMetadata(timeStamp), cancel);
                }

                if (option.IsInboxUploadMode)
                {
                    result.IsUploadingForInbox = true;

                    // 電子メールの送信
                    if (existingSecureJson!.InboxEmail._IsFilled())
                    {
                        var db = this.DbSnapshot;

                        string body = emailBody.ToString();
                        string subject = emailSubject;
                        string from = db.SmtpFrom._NonNullTrim();
                        string to = existingSecureJson!.InboxEmail;

                        if (from._IsFilled() && to._IsFilled() && db.SmtpHostname._IsFilled() && db.SmtpPort != 0)
                        {
                            try
                            {
                                if (await SmtpUtil.SendAsync(new SmtpConfig(db.SmtpHostname, db.SmtpPort, db.SmtpUseSsl._ToBool(), db.SmtpUsername, db.SmtpPassword), from, to, subject, body, false, cancel))
                                {
                                    string email = to;
                                    int index = email.IndexOf('@');
                                    if (index != -1)
                                    {
                                        email = '*'._MakeCharArray(index) + email.Substring(index);
                                    }
                                    result.EmailSentForInbox = email;
                                }
                            }
                            catch (Exception ex)
                            {
                                ex._Error();
                            }
                        }
                    }
                }

                Stat.AddReport("UploadedFilesSize_Total", result.TotalFileSize);
                Stat.AddReport("UploadedFilesCount_Total", result.NumFiles);
                Stat.AddReport("UploadedRequests_Total", 1);

                if (result.IsCreatingUploadInbox)
                {
                    Stat.AddReport("CreatedUploadInbox_Total", 1);
                }

                return result;
            }
            catch
            {
                // 何らかの処理に失敗した場合はディレクトリごと削除する
                try
                {
                    if (option.IsInboxUploadMode == false)
                    {
                        await Lfs.DeleteDirectoryAsync(newDirFullPath, true);
                    }
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
                this.Stat._DisposeSafe();

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


