using MedicalResearch.Workflow;
using MedicalResearch.Workflow.Model;
using MedicalResearch.Workflow.Persistence.EF;
using MedicalResearch.Workflow.StoreAccess;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Web.UJMW;

namespace MedicalResearch.StudyWorkflowDefinition.WebAPI {

  public class Startup {

    public Startup(IConfiguration configuration) {
      _Configuration = configuration;
      WorkflowDefinitionDbContext.ConnectionString = configuration.GetValue<String>("SqlConnectionString");
    }

    private static IConfiguration _Configuration = null;
    public static IConfiguration Configuration { get { return _Configuration; } }

    const string _ApiTitle = "ORSCF StudyWorkflowDefinition";
    Version _ApiVersion = null;

    public void ConfigureServices(IServiceCollection services) {
      services.AddLogging();

      _ApiVersion = Version.Parse(ApiVersion.SemanticVersion);

      //WorkflowDefinitionDbContext.Migrate();

      string outDir = AppDomain.CurrentDomain.BaseDirectory;

      var apiService = new ApiService(
        _Configuration.GetValue<string>("OAuthTokenRequestUrl"),
        _Configuration.GetValue<string>("PublicServiceUrl"),
        _Configuration.GetValue<string>("SubscriptionStorageDirectory")
      );

      services.AddSingleton<IWdrApiInfoService>(apiService);

      WdrShowcaseEndpointFactory.GetFactoryMethodsPerEndpoint((contractType, factory) => {
        services.AddSingleton(contractType, (s) => factory());
      });

      services.AddDynamicUjmwControllers(
        (r) => {

          r.AddControllerFor<IWdrApiInfoService>("wdr/v2/WdrApiInfo");

          WdrEndpointRegister.GetContractsPerEndpoint((contractType, subroute) => {
            r.AddControllerFor(contractType, "wdr/v2/" + subroute);
          });

        }
      );

      services.AddSwaggerGen(c => {

        c.EnableAnnotations(true, true);

        c.IncludeXmlComments(outDir + "Hl7.Fhir.R4.xml", true);
        c.IncludeXmlComments(outDir + "ORSCF.StudyWorkflowDefinition.Contract.xml", true);
        c.IncludeXmlComments(outDir + "ORSCF.StudyWorkflowDefinition.Service.xml", true);
        c.IncludeXmlComments(outDir + "ORSCF.StudyWorkflowDefinition.Service.WebAPI.xml", true);
        c.IncludeXmlComments(outDir + "FUSE-fx.RepositoryContract.xml", true);

        #region bearer

        //https://www.thecodebuzz.com/jwt-authorization-token-swagger-open-api-asp-net-core-3-0/
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
          Name = "Authorization",
          Type = SecuritySchemeType.ApiKey,
          Scheme = "Bearer",
          BearerFormat = "JWT",
          In = ParameterLocation.Header,
          Description = "JWT Authorization header using the Bearer scheme."
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
          {
              {
                    new OpenApiSecurityScheme
                      {
                          Reference = new OpenApiReference
                          {
                              Type = ReferenceType.SecurityScheme,
                              Id = "Bearer"
                          }
                      },
                      new string[] {}

              }
          });

        #endregion

        c.UseInlineDefinitionsForEnums();

        c.SwaggerDoc(
          "ApiV" + _ApiVersion.ToString(1),
          new OpenApiInfo {
            Title = _ApiTitle + "-API",
            Version = _ApiVersion.ToString(3),
            Description = "NOTE: This is not intended be a 'RESTful' api, as it is NOT located on the persistence layer and is therefore NOT focused on doing CRUD operations! This HTTP-based API uses a 'call-based' approach to known BL operations. IN-, OUT- and return-arguments are transmitted using request-/response- wrappers (see [UJMW](https://github.com/KornSW/UnifiedJsonMessageWrapper)), which are very lightweight and are a compromise for broad support and adaptability in REST-inspired technologies as well as soap-inspired technologies!",
            Contact = new OpenApiContact {
              Name = "Open Research Study Communication Format",
              Email = "info@orscf.org",
              Url = new Uri("https://orscf.org")
            },
          }
        );

      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerfactory) {

      var logFileFullName = _Configuration.GetValue<string>("LogFileName");
      var logDir = Path.GetFullPath(Path.GetDirectoryName(logFileFullName));
      Directory.CreateDirectory(logDir);
      loggerfactory.AddFile(logFileFullName);

      //required for the www-root
      app.UseStaticFiles();

      if (!_Configuration.GetValue<bool>("ProdMode")) {
        app.UseDeveloperExceptionPage();
      }

      if (_Configuration.GetValue<bool>("EnableSwaggerUi")) {
        var baseUrl = _Configuration.GetValue<string>("BaseUrl");

        app.UseSwagger(o => {
          //warning: needs subfolder! jsons cant be within same dir as swaggerui (below)
          o.RouteTemplate = "docs/schema/{documentName}.swagger.{json|yaml}";
          //o.SerializeAsV2 = true;
        });

        app.UseSwaggerUI(c => {

          c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
          c.DefaultModelExpandDepth(2);
          c.DefaultModelsExpandDepth(2);
          //c.ConfigObject.DefaultModelExpandDepth = 2;

          c.DocumentTitle = _ApiTitle + " - OpenAPI Definition(s)";

          //represents the sorting in SwaggerUI combo-box
          c.SwaggerEndpoint($"schema/ApiV{_ApiVersion.ToString(1)}.swagger.json", $"{_ApiTitle}-API v{_ApiVersion.ToString(3)}");

          c.RoutePrefix = "docs";

          //requires MVC app.UseStaticFiles();
          c.InjectStylesheet(baseUrl + "swagger-ui/custom.css");

        });

      }

      app.UseHttpsRedirection();

      app.UseRouting();

      app.UseAuthorization();

      app.UseEndpoints(endpoints => {
        endpoints.MapControllers();
      });

    }
  }
}
