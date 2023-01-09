/********************************************************************************
 * Copyright (c) 2021,2022 BMW Group AG
 * Copyright (c) 2021,2022 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Token;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Web;
using Org.Eclipse.TractusX.Portal.Backend.Mailing.SendMail;
using Org.Eclipse.TractusX.Portal.Backend.Notifications.Library;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Service;
using Microsoft.Extensions.FileProviders;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library.DependencyInjection;

var VERSION = "v2";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Kubernetes")
{
    var provider = new PhysicalFileProvider("/app/secrets");
    builder.Configuration.AddJsonFile(provider, "appsettings.json", optional: false, reloadOnChange: false);
}

builder.Services.AddDefaultServices<Program>(builder.Configuration, VERSION)
                .AddMailingAndTemplateManager(builder.Configuration)
                .AddPortalRepositories(builder.Configuration)
                .AddProvisioningManager(builder.Configuration);

builder.Services.AddTransient<IUserProvisioningService, UserProvisioningService>();

builder.Services.AddTransient<IInvitationBusinessLogic, InvitationBusinessLogic>()
                .ConfigureInvitationSettings(builder.Configuration.GetSection("Invitation"));

builder.Services.AddTransient<IUserBusinessLogic, UserBusinessLogic>()
                .AddTransient<IUserUploadBusinessLogic, UserUploadBusinessLogic>()
                .AddTransient<IUserRolesBusinessLogic, UserRolesBusinessLogic>()
                .ConfigureUserSettings(builder.Configuration.GetSection("UserManagement"));

builder.Services.AddTransient<IRegistrationBusinessLogic, RegistrationBusinessLogic>()
                .ConfigureRegistrationSettings(builder.Configuration.GetSection("Registration"));

builder.Services.AddTransient<IServiceAccountBusinessLogic, ServiceAccountBusinessLogic>()
                .ConfigureServiceAccountSettings(builder.Configuration.GetSection("ServiceAccount"));

builder.Services.AddTransient<IDocumentsBusinessLogic, DocumentsBusinessLogic>()
    .ConfigureDocumentSettings(builder.Configuration.GetSection("Document"));
builder.Services.AddTransient<IStaticDataBusinessLogic, StaticDataBusinessLogic>();
builder.Services.AddTransient<IPartnerNetworkBusinessLogic, PartnerNetworkBusinessLogic>();
builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<ICompanyDataBusinessLogic, CompanyDataBusinessLogic>();

builder.Services.AddTransient<IIdentityProviderBusinessLogic, IdentityProviderBusinessLogic>()
                .ConfigureIdentityProviderSettings(builder.Configuration.GetSection("IdentityProviderAdmin"));

builder.Services.AddTransient<ITokenService, TokenService>();

builder.Services
    .AddSdFactoryService(builder.Configuration.GetSection("SdFactory"))
    .AddDapsService(builder.Configuration.GetSection("Daps"))
    .AddChecklist(builder.Configuration.GetSection("Bpdm"), builder.Configuration.GetSection("Custodian"));

builder.Services.AddTransient<IConnectorsBusinessLogic, ConnectorsBusinessLogic>()
                .ConfigureConnectorsSettings(builder.Configuration.GetSection("Connectors"));

builder.Services.AddTransient<IServiceProviderBusinessLogic, ServiceProviderBusinessLogic>();

builder.Build()
    .CreateApp<Program>("administration", VERSION)
    .Run();
