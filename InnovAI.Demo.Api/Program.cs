using Hangfire;
using Hangfire.InMemory;
using Microsoft.EntityFrameworkCore;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using InnovAI.Demo.Api.Data;
using InnovAI.Demo.Api.Interfaces;
using InnovAI.Demo.Api.Services;
using InnovAI.Demo.Api.Settings;
using InnovAI.Demo.Api.Stubs;
using System.ClientModel;
using System.Net.Http.Headers;
using System.Text;

namespace InnovAI.Demo.Api;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/demo-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("InnovAI.Demo starting up...");

            var builder = WebApplication.CreateBuilder(args);
            // writeToProviders: true → Serilog propaga anche agli ILoggerProvider registrati (es. OpenTelemetry)
            builder.Host.UseSerilog((ctx, lc) => lc
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
                .ReadFrom.Configuration(ctx.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/demo-.log", rollingInterval: RollingInterval.Day),
                writeToProviders: true);

            ConfigureServices(builder);

            var app = builder.Build();

            // Crea il DB locale al primo avvio — abilitare solo in sviluppo locale,
            // non necessario quando si punta al DB Azure del cliente.
            if (app.Configuration.GetValue<bool>("AppSettings:EnsureDbCreated"))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
                db.Database.EnsureCreated();
            }

            ConfigureApp(app);

            app.Run();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        // Settings
        builder.Services.Configure<DemoSettings>(configuration.GetSection("Demo"));
        builder.Services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        builder.Services.Configure<AiAgentCrmSettings>(configuration.GetSection("AiAgentCrm"));
        builder.Services.Configure<AzureOpenAISettings>(configuration.GetSection("AzureOpenAI"));

        // DB
        builder.Services.AddDbContext<DemoDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        var aiSettings = configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>()
            ?? throw new InvalidOperationException("Sezione AzureOpenAI mancante in appsettings");

// Azure OpenAI: IChatClient per l'agente (Microsoft Agent Framework) + embedding generator per la ricerca vettoriale
        var chatClient = new AzureOpenAIClient(
                new Uri(aiSettings.ChatCompletion.Endpoint),
                new ApiKeyCredential(aiSettings.ChatCompletion.ApiKey))
            .GetChatClient(aiSettings.ChatCompletion.Deployment)
            .AsIChatClient();
        builder.Services.AddSingleton(chatClient);

        var embeddingGenerator = new AzureOpenAIClient(
                new Uri(aiSettings.Embedding.Endpoint),
                new ApiKeyCredential(aiSettings.Embedding.ApiKey))
            .GetEmbeddingClient(aiSettings.Embedding.Deployment)
            .AsIEmbeddingGenerator(aiSettings.Embedding.Dimensions);
        builder.Services.AddSingleton(embeddingGenerator);

        // Stubs
        builder.Services.AddScoped<IBusinessConfigurationService, NullBusinessConfigurationService>();
        builder.Services.AddScoped<ITextAnalysisService, NullTextAnalysisService>();

        // CRM: mock (demo) o reale in base a Demo:UseMockCrm
        var crmSettings = configuration.GetSection("CrmApi").Get<CrmApiSettings>() ?? new CrmApiSettings();
        builder.Services.Configure<CrmApiSettings>(configuration.GetSection("CrmApi"));

        var useMockCrm = configuration.GetValue<bool>("Demo:UseMockCrm", defaultValue: true);
        if (useMockCrm)
        {
            builder.Services.AddScoped<ICrmProxyService, DemoCrmProxyService>();
        }
        else
        {
            builder.Services.AddHttpClient("CrmApi", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(crmSettings.TimeoutSeconds);
                if (!string.IsNullOrEmpty(crmSettings.Username))
                {
                    var basic = Convert.ToBase64String(
                        Encoding.ASCII.GetBytes($"{crmSettings.Username}:{crmSettings.Password}"));
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", basic);
                }
            });
            builder.Services.AddScoped<ICrmProxyService, CrmProxyService>();
        }

        // Core services
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddScoped<TokenizerService>();
        builder.Services.AddScoped<DocumentService>();
        builder.Services.AddScoped<VectorSearchService>();
        builder.Services.AddSingleton<TemplateProcessor>();
        builder.Services.AddScoped<IAiAgentCrmService, AiAgentCrmService>();
        builder.Services.AddScoped<ISendEmailService, SendEmailJob>();
        builder.Services.AddScoped<SendEmailJob>();

        // Hangfire in-memory (ideale per demo; usare SqlServer per produzione)
        builder.Services.AddHangfire(cfg =>
            cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
               .UseSimpleAssemblyNameTypeSerializer()
               .UseRecommendedSerializerSettings()
               .UseInMemoryStorage(new InMemoryStorageOptions { MaxExpirationTime = TimeSpan.FromHours(4) }));
        builder.Services.AddHangfireServer();

        builder.Services.AddHttpClient();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "InnovAI Demo API", Version = "v1" });
            c.AddSecurityDefinition("ApiKey", new()
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Name = "X-Api-Key",
                Description = "API Key per /api/similarity (anche Bearer token)"
            });
            c.AddSecurityRequirement(new()
            {
                {
                    new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" } },
                    []
                }
            });
        });

        builder.Services.AddCors(opt =>
            opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        // OpenTelemetry → Aspire Dashboard (attivo solo se OpenTelemetry:Endpoint è valorizzato)
        var otlpEndpoint = configuration["OpenTelemetry:Endpoint"];
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService("InnovAI.Demo.Api"))
                .WithTracing(t => t
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
                .WithMetrics(m => m
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
                .WithLogging(l => l
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));
        }
    }

    private static void ConfigureApp(WebApplication app)
    {
        app.UseSerilogRequestLogging();

        app.UseSwagger();
        app.UseSwaggerUI(options =>
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "InnovAI Demo API v1"));

        app.UseCors("AllowAll");
        app.UseHangfireDashboard("/jobs");
        app.MapHangfireDashboard();
        app.MapControllers();

        app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
           .WithTags("Health");
    }
}
