using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

using IPA.Cores.Web;
using IPA.Cores.Helper.Web;
using static IPA.Cores.Globals.Web;

using IPA.Cores.Codes;
using IPA.Cores.Helper.Codes;
using static IPA.Cores.Globals.Codes;

namespace APP_NAME_HERE
{
    public class Startup
    {
        readonly HttpServerStartupHelper StartupHelper;
        readonly AspNetLib AspNetLib;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            // HttpServer �w���p�[�̏�����
            StartupHelper = new HttpServerStartupHelper(configuration);

            // AspNetLib �̏�����: �K�v�ȋ@�\�̂� ON �ɂ��邱��
            AspNetLib = new AspNetLib(configuration, AspNetLibFeatures.None);
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // AspNetLib �ɂ��ݒ��ǉ�
            AspNetLib.ConfigureServices(StartupHelper, services);

            // ��{�I�Ȑݒ��ǉ�
            StartupHelper.ConfigureServices(services);

            // ���N�G�X�g�������@�\��ǉ�
            services.AddHttpRequestRateLimiter<HttpRequestRateLimiterHashKeys.SrcIPAddress>(_ => { });
            
            ////// Cookie �F�؋@�\��ǉ�
            //EasyCookieAuth.LoginFormMessage.TrySet("���O�C�����K�v�ł��B");
            //EasyCookieAuth.AuthenticationPasswordValidator = StartupHelper.SimpleBasicAuthenticationPasswordValidator;
            //EasyCookieAuth.ConfigureServices(services, !StartupHelper.ServerOptions.AutomaticRedirectToHttpsIfPossible);

            // MVC �@�\��ǉ�
            services.AddControllersWithViews()
                .ConfigureMvcWithAspNetLib(AspNetLib);

            // �V���O���g���T�[�r�X�̒���
            //services.AddSingleton(new Server());

            // �S�y�[�W���ʃR���e�L�X�g�̒���
            //services.AddScoped<PageContext>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            // ���N�G�X�g������
            app.UseHttpRequestRateLimiter<HttpRequestRateLimiterHashKeys.SrcIPAddress>();

            // wwwroot �f�B���N�g���� static �t�@�C���̃��[�g�Ƃ��Ēǉ�
            StartupHelper.AddStaticFileProvider(Env.AppRootDir._CombinePath("wwwroot"));

            // AspNetLib �ɂ��ݒ��ǉ�
            AspNetLib.Configure(StartupHelper, app, env);

            // ��{�I�Ȑݒ��ǉ�
            StartupHelper.Configure(app, env);

            // �G���[�y�[�W��ǉ�
            if (StartupHelper.IsDevelopmentMode)
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            // �G���[���O��ǉ�
            app.UseHttpExceptionLogger();

            // Static �t�@�C����ǉ�
            app.UseStaticFiles();

            // ���[�e�B���O��L���� (�F�؂𗘗p����ꍇ�͔F�ؑO�ɌĂяo���K�v������)
            app.UseRouting();

            // �F�؁E�F�����{
            app.UseAuthentication();
            app.UseAuthorization();

            // ���[�g�}�b�v���`
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            // �N���[���A�b�v������`
            lifetime.ApplicationStopping.Register(() =>
            {
                //server._DisposeSafe();

                AspNetLib._DisposeSafe();
                StartupHelper._DisposeSafe();
            });
        }
    }
}
