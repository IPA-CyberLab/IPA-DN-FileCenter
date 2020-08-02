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

        public const string FileBrowserHttpDir = "/files";

        public const string DefaultWebSiteTitle = "FileCenter";
    }

    public class DbHive : INormalizable
    {
        public string DataStoreRootDir = "";
        public string PIN = "";
        public string WebSiteTitle = "";

        public void Normalize()
        {
            if (DataStoreRootDir._IsEmpty()) DataStoreRootDir = FileCenterConsts.DefaultStoreRoot;

            if (WebSiteTitle._IsEmpty()) WebSiteTitle = FileCenterConsts.DefaultWebSiteTitle;
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


