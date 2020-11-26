using Autofac;
using CExtension.Authentication;
using CExtension.Autofac;
using CExtension.Context;
using CExtension.Permission;
using CExtension.Swagger;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace CAPI
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // �ο�:https://github.com/autofac/Examples/blob/master/src/AspNetCore3Example/Startup.cs
        public void ConfigureContainer(ContainerBuilder builder)
        {
            // Add any Autofac modules or registrations.
            // This is called AFTER ConfigureServices so things you
            // register here OVERRIDE things registered in ConfigureServices.
            //
            // You must have the call to `UseServiceProviderFactory(new AutofacServiceProviderFactory())`
            // when building the host or this won't be called.
            builder.RegisterModule(new AutofacModule());
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddMediatR(typeof(Startup));

            services.ContextServiceSetup();

            services.ConfigureSwaggerService();

            services.ConfigureAuthService();

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                    .SetIsOriginAllowed((host) => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });
            //ConfigureAuthService(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.ConfigureSwagger();

            // ��֤
            app.UseAuthentication();

            // ��Ȩ
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void ConfigureAuthService(IServiceCollection services)
        {
            var identityUrl = "https://localhost:5001";

            //services.AddAuthentication(options =>
            //{
            //    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            //    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            //    //options.DefaultChallengeScheme = nameof(ResponseHandler);
            //    //options.DefaultForbidScheme = nameof(ResponseHandler);
            //}).AddJwtBearer(options =>
            //{
            //    options.Authority = identityUrl;
            //    options.RequireHttpsMetadata = false;
            //    options.Audience = "api1";
            //});
            //.AddScheme<AuthenticationSchemeOptions, ResponseHandler>(nameof(ResponseHandler), o => { });

            // ��������д��һ��,��Ϊ�ܶ��˿�����
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            //��һ����֤д��������ɹ���¼������ӿڻ��ǻ᷵��401
            //services.AddAuthentication(o =>
            //{
            //    o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            //    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            //    o.DefaultChallengeScheme = nameof(ResponseHandler);
            //    o.DefaultForbidScheme = nameof(ResponseHandler);
            //})
            //.AddJwtBearer(options =>
            //{
            //    options.Authority = "https://localhost:5001";
            //    options.RequireHttpsMetadata = false;
            //    options.Audience = "api1";
            //})
            //.AddScheme<AuthenticationSchemeOptions, ResponseHandler>(nameof(ResponseHandler), o => { });

            //services
            //    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            //    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            //    {
            //        options.Authority = "https://localhost:5001";
            //        options.RequireHttpsMetadata = false;
            //        //options.Audience = "CAPI";
            //        options.TokenValidationParameters = new TokenValidationParameters { ValidateAudience = false };
            //    })
            //    .AddScheme<AuthenticationSchemeOptions, ResponseHandler>(nameof(ResponseHandler), o => { });

            //��һ����֤д�����ɹ���¼�Ϳ�������ӿ�
            services
                .AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = identityUrl;
                    options.TokenValidationParameters = new TokenValidationParameters { ValidateAudience = false };
                })
                .AddScheme<AuthenticationSchemeOptions, AuthenticationHandler>(nameof(AuthenticationHandler), o => { });

            //��Ȩ
            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiScope", policy =>
                {
                    //policy.RequireAuthenticatedUser();
                    //policy.RequireClaim("scope", "api1");
                    policy.RequireScope("api");
                });
            });

            // ע��Ȩ�޴�����
            services.AddScoped<IAuthorizationHandler, PermissionHandler>();

            #region ����
            //��ȡ�����ļ�
            var symmetricKeyAsBase64 = "secret";
            var keyByteArray = Encoding.ASCII.GetBytes(symmetricKeyAsBase64);
            var signingKey = new SymmetricSecurityKey(keyByteArray);
            var Issuer = "API";
            var Audience = "wr";

            var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            // ���Ҫ���ݿ⶯̬�󶨣������������գ���ߴ������ﶯ̬��ֵ
            var permission = new List<PermissionItem>();

            // ��ɫ��ӿڵ�Ȩ��Ҫ�����
            var permissionRequirement = new PermissionRequirement(
                "/api/denied",// �ܾ���Ȩ����ת��ַ��Ŀǰ���ã�
                permission,
                ClaimTypes.Role,//���ڽ�ɫ����Ȩ
                Issuer,//������
                Audience,//����
                signingCredentials,//ǩ��ƾ��
                expiration: TimeSpan.FromSeconds(60 * 60)//�ӿڵĹ���ʱ��
                );
            #endregion


            //// 3���Զ��帴�ӵĲ�����Ȩ
            //services.AddAuthorization(options =>
            //{
            //    options.AddPolicy("permission",
            //             policy => policy.Requirements.Add(permissionRequirement));
            //});


            //// 4������Scope������Ȩ
            //services.AddAuthorization(options =>
            //{
            //    options.AddPolicy("Scope_BlogModule_Policy", builder =>
            //    {
            //        //�ͻ���Scope�а���blog.core.api.BlogModule���ܷ���
            //        builder.RequireScope("blog.core.api.BlogModule");
            //    });

            //    // ���� Scope ����
            //    // ...

            //});

            //services.AddSingleton(permissionRequirement);
        }
    }

    //public static class CustomConfig
    //{
    //    public static void ConfigureSwaggerService(this IServiceCollection services)
    //    {

    //    }
    //}
}
