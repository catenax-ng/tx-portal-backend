/********************************************************************************
 * Copyright (c) 2021, 2023 BMW Group AG
 * Copyright (c) 2021, 2023 Contributors to the Eclipse Foundation
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

using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.ViewModels;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models;
using Org.Eclipse.TractusX.Portal.Backend.Notifications.Library;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Service;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared;
using Xunit;

namespace Org.Eclipse.TractusX.Portal.Backend.Apps.Service.BusinessLogic.Tests;

public class AppReleaseBusinessLogicTest
{
    private readonly IFixture _fixture;
    private readonly IPortalRepositories _portalRepositories;
    private readonly IOfferRepository _offerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserRolesRepository _userRolesRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IOptions<AppsSettings> _options;
    private readonly CompanyUser _companyUser;
    private readonly IamUser _iamUser;
    private readonly IOfferService _offerService;
    private readonly INotificationService _notificationService;
    private readonly Guid _notExistingAppId = Guid.NewGuid();
    private readonly Guid _activeAppId = Guid.NewGuid();
    private readonly Guid _differentCompanyAppId = Guid.NewGuid();
    private readonly Guid _existingAppId = Guid.NewGuid();
    private readonly ILanguageRepository _languageRepository;
    private readonly AppsSettings _settings;
    private const string ClientId = "catenax-portal";
    private static readonly Guid ValidDocumentId = Guid.NewGuid();
    private static readonly string IamUserId = Guid.NewGuid().ToString();

    public AppReleaseBusinessLogicTest()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _portalRepositories = A.Fake<IPortalRepositories>();
        _offerRepository = A.Fake<IOfferRepository>();
        _userRolesRepository = A.Fake<IUserRolesRepository>();
        _userRepository = A.Fake<IUserRepository>();
        _documentRepository = A.Fake<IDocumentRepository>();
        _languageRepository = A.Fake<ILanguageRepository>();
        _offerService = A.Fake<IOfferService>();
        _notificationService = A.Fake<INotificationService>();
        _options = A.Fake<IOptions<AppsSettings>>();
        _companyUser = _fixture.Build<CompanyUser>()
            .Without(u => u.IamUser)
            .Create();
        _iamUser = _fixture.Build<IamUser>()
            .With(u => u.CompanyUser, _companyUser)
            .Create();
        _companyUser.IamUser = _iamUser;
        
        _settings = A.Fake<AppsSettings>();
        _settings.OfferStatusIds = new [] 
        {
            OfferStatusId.IN_REVIEW,
            OfferStatusId.ACTIVE
        };
        _settings.ActiveAppNotificationTypeIds = new []
        {
            NotificationTypeId.APP_ROLE_ADDED
        };
        _settings.SubmitAppNotificationTypeIds = new []
        {
            NotificationTypeId.APP_RELEASE_REQUEST
        };
         _settings.ActiveAppCompanyAdminRoles = new Dictionary<string, IEnumerable<string>>
        {
            { ClientId, new [] { "Company Admin" } }
        };
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>()).Returns(_offerRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IUserRepository>()).Returns(_userRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IUserRolesRepository>()).Returns(_userRolesRepository);
         A.CallTo(() => _portalRepositories.GetInstance<IDocumentRepository>()).Returns(_documentRepository);
    }

    [Fact]
    public async Task CreateServiceOffering_WithValidDataAndEmptyDescriptions_ReturnsCorrectDetails()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var iamUserId = _fixture.Create<string>();
        var appUserRoleDescription = new AppUserRoleDescription[] {
            new("en","this is test1"),
            new("de","this is test2"),
            new("fr","this is test3")
        };
        var appUserRoles = new AppUserRole[] {
            new("IT Admin",appUserRoleDescription)
        };
        A.CallTo(() => _offerRepository.IsProviderCompanyUserAsync(A<Guid>.That.IsEqualTo(appId), A<string>.That.IsEqualTo(iamUserId), A<OfferTypeId>.That.IsEqualTo(OfferTypeId.APP))).Returns((true,true));
        var sut = new AppReleaseBusinessLogic(_portalRepositories, _options, null!, null!);

        // Act
        var result = await sut.AddAppUserRoleAsync(appId, appUserRoles, iamUserId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _offerRepository.IsProviderCompanyUserAsync(A<Guid>._, A<string>._, A<OfferTypeId>._)).MustHaveHappened();
        foreach(var appRole in appUserRoles)
        {
            A.CallTo(() => _userRolesRepository.CreateAppUserRole(A<Guid>._, A<string>.That.IsEqualTo(appRole.role))).MustHaveHappened();
            foreach(var item in appRole.descriptions)
            {
                A.CallTo(() => _userRolesRepository.CreateAppUserRoleDescription(A<Guid>._, A<string>.That.IsEqualTo(item.languageCode), A<string>.That.IsEqualTo(item.description))).MustHaveHappened();
            }
        }

        Assert.NotNull(result);
        Assert.IsAssignableFrom<IEnumerable<AppRoleData>>(result);
    }

    #region AddAppAsync
    
    [Fact]
    public async Task AddAppAsync_WithoutEmptyLanguageCodes_ThrowsException()
    {
        // Arrange
        var data = new AppRequestModel("test", "test", Guid.NewGuid(), new List<Guid>(), new List<LocalizedDescription>(), new [] { string.Empty }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA  
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
     
        // Act
        async Task Act() => await sut.AddAppAsync(data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        var error = await Assert.ThrowsAsync<ControllerArgumentException>(Act).ConfigureAwait(false);
        error.ParamName.Should().Be("SupportedLanguageCodes");
    }
    
    [Fact]
    public async Task AddAppAsync_WithEmptyUseCaseIds_ThrowsException()
    {
        // Arrange
        var data = new AppRequestModel("test", "test", Guid.NewGuid(), new []{ Guid.Empty }, new List<LocalizedDescription>(), new [] { "de" }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA  
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
     
        // Act
        async Task Act() => await sut.AddAppAsync(data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        var error = await Assert.ThrowsAsync<ControllerArgumentException>(Act).ConfigureAwait(false);
        error.ParamName.Should().Be("UseCaseIds");
    }

    [Fact]
    public async Task AddAppAsync_WithSalesManagerValidData_ReturnsExpected()
    {
        // Arrange
        var data = new AppRequestModel("test", "test", _companyUser.Id, new []{ Guid.NewGuid() }, new List<LocalizedDescription>{ new("de", "Long description", "desc")}, new [] { "de" }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA   
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
     
        // Act
        await sut.AddAppAsync(data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _offerService.ValidateSalesManager(A<Guid>._, A<string>._, A<IDictionary<string, IEnumerable<string>>>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.CreateOffer(A<string>._, A<OfferTypeId>._, A<Action<Offer>?>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.AddOfferDescriptions(A<IEnumerable<(Guid appId, string languageShortName, string descriptionLong, string descriptionShort)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.AddAppLanguages(A<IEnumerable<(Guid appId, string languageShortName)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.AddAppAssignedUseCases(A<IEnumerable<(Guid appId, Guid useCaseId)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.CreateOfferLicenses(A<string>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.CreateOfferAssignedLicense(A<Guid>._, A<Guid>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AddAppAsync_WithNullSalesMangerValidData_ReturnsExpected()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetOwnCompanyId(A<string>.That.IsEqualTo(_iamUser.UserEntityId))).Returns(_companyUser.CompanyId);
        var data = new AppRequestModel("test", "test", null, new []{ Guid.NewGuid() }, new List<LocalizedDescription>{ new("de", "Long description", "desc")}, new [] { "de" }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA   
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
     
        // Act
        await sut.AddAppAsync(data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _offerService.ValidateSalesManager(A<Guid>._, A<string>._, A<IDictionary<string, IEnumerable<string>>>._)).MustNotHaveHappened();
        A.CallTo(() => _userRepository.GetOwnCompanyId(_iamUser.UserEntityId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.CreateOffer(A<string>._, A<OfferTypeId>._, A<Action<Offer>?>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.AddOfferDescriptions(A<IEnumerable<(Guid appId, string languageShortName, string descriptionLong, string descriptionShort)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.AddAppLanguages(A<IEnumerable<(Guid appId, string languageShortName)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.AddAppAssignedUseCases(A<IEnumerable<(Guid appId, Guid useCaseId)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.CreateOfferLicenses(A<string>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.CreateOfferAssignedLicense(A<Guid>._, A<Guid>._))
            .MustHaveHappenedOnceExactly();
    }

    #endregion
    
    #region UpdateAppReleaseAsync
    
    [Fact]
    public async Task UpdateAppReleaseAsync_WithoutApp_ThrowsException()
    {
        // Arrange
        SetupUpdateApp();
        var data = new AppRequestModel("test", "test", Guid.NewGuid(), new List<Guid>(), new List<LocalizedDescription>(), new [] { string.Empty }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA   
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
     
        // Act
        async Task Act() => await sut.UpdateAppReleaseAsync(_notExistingAppId, data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        var error = await Assert.ThrowsAsync<NotFoundException>(Act).ConfigureAwait(false);
        error.Message.Should().Be($"App {_notExistingAppId} does not exists");
    }
    
    [Fact]
    public async Task UpdateAppReleaseAsync_WithActiveApp_ThrowsException()
    {
        // Arrange
        SetupUpdateApp();
        var data = new AppRequestModel("test", "test", Guid.NewGuid(), new []{ Guid.Empty }, new List<LocalizedDescription>(), new [] { "de" }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA  
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
     
        // Act
        async Task Act() => await sut.UpdateAppReleaseAsync(_activeAppId, data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        var error = await Assert.ThrowsAsync<ConflictException>(Act).ConfigureAwait(false);
        error.Message.Should().Be("Apps in State ACTIVE can't be updated");
    }

    [Fact]
    public async Task UpdateAppReleaseAsync_WithInvalidUser_ThrowsException()
    {
        // Arrange
        SetupUpdateApp();
        var data = new AppRequestModel("test", "test", Guid.NewGuid(), new []{ Guid.NewGuid() }, new List<LocalizedDescription>(), new [] { "de" }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA  
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
     
        // Act
        async Task Act() => await sut.UpdateAppReleaseAsync(_differentCompanyAppId, data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(Act).ConfigureAwait(false);
        error.Message.Should().Be($"User {_iamUser.UserEntityId} is not allowed to change the app.");
    }

    [Fact]
    public async Task UpdateAppReleaseAsync_WithInvalidLanguage_ThrowsException()
    {
        // Arrange
        SetupUpdateApp();
        var data = new AppRequestModel("test", "test", _companyUser.Id, new []{ Guid.NewGuid() }, new List<LocalizedDescription>(), new [] { "de", "en", "invalid" }, "123", new[]
            {
                PrivacyPolicyId.COMPANY_DATA  
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
        
        // Act
        async Task Act() => await sut.UpdateAppReleaseAsync(_existingAppId, data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        var error = await Assert.ThrowsAsync<ControllerArgumentException>(Act).ConfigureAwait(false);
        error.ParamName.Should().Be("SupportedLanguageCodes");
    }

    [Fact]
    public async Task UpdateAppReleaseAsync_WithValidData_ReturnsExpected()
    {
        // Arrange
        SetupUpdateApp();

        var data = new AppRequestModel(
            "test",
            "test",  
            _companyUser.Id, 
            new [] { Guid.NewGuid() },
            new LocalizedDescription[]
            {
               new("de", "Long description", "desc") 
            }, 
            new [] { "de", "en" },
            "43",
            new[]
            {
                PrivacyPolicyId.COMPANY_DATA  
            });
        var settings = new AppsSettings();
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);
        
        // Act
        await sut.UpdateAppReleaseAsync(_existingAppId, data, _iamUser.UserEntityId).ConfigureAwait(false);
        
        // Assert
        A.CallTo(() => _offerRepository.AttachAndModifyOffer(A<Guid>._, A<Action<Offer>>._, A<Action<Offer>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerService.UpsertRemoveOfferDescription(_existingAppId, A<IEnumerable<Localization>>.That.IsSameSequenceAs(new Localization("de", "Long description", "desc")), A<IEnumerable<OfferDescriptionData>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.AddAppLanguages(A<IEnumerable<(Guid appId, string languageShortName)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.RemoveAppLanguages(A<IEnumerable<(Guid,string)>>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.CreateDeleteAppAssignedUseCases(_existingAppId, A<IEnumerable<Guid>>.That.IsEmpty(), A<IEnumerable<Guid>>.That.Matches(x => x.SequenceEqual(data.UseCaseIds))))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerService.CreateOrUpdateOfferLicense(_existingAppId, A<string>._, A<(Guid OfferLicenseId, string LicenseText, bool AssingedToMultipleOffers)>._)).MustHaveHappenedOnceExactly();
    }

    #endregion
    
    #region Create App Document
        
    [Fact]
    public async Task CreateAppDocumentAsync_ExecutesSuccessfully()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var file = FormFileHelper.GetFormFile("this is just a test", "superFile.pdf", "application/pdf");

        var settings = new AppsSettings()
        {
            ContentTypeSettings = new[] { "application/pdf" },
            DocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        // Act
        await sut.CreateAppDocumentAsync(appId, DocumentTypeId.APP_CONTRACT, file, _iamUser.UserEntityId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _offerService.UploadDocumentAsync(appId, DocumentTypeId.APP_CONTRACT, file, _iamUser.UserEntityId, OfferTypeId.APP, settings.DocumentTypeIds, settings.ContentTypeSettings, CancellationToken.None)).MustHaveHappenedOnceExactly();
    }
    
    #endregion

    #region  AddActiveAppUserRole

    [Fact]
    public async Task AddActiveAppUserRoleAsync_ExecutesSuccessfully()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();
        var appName = _fixture.Create<string>();

        var appUserRoleDescription = new AppUserRoleDescription[] {
            new("de","this is test1"),
            new("en","this is test2"),
        };
        var appAssignedRoleDesc = new AppUserRole[] { new("Legal Admin", appUserRoleDescription) };
       
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>().GetOfferNameProviderCompanyUserAsync(appId, _iamUser.UserEntityId, OfferTypeId.APP))
            .ReturnsLazily(() => (true, appName, _companyUser.Id, companyId));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(_settings), _offerService, _notificationService);

        //Act
        var result = await sut.AddActiveAppUserRoleAsync(appId, appAssignedRoleDesc, _iamUser.UserEntityId).ConfigureAwait(false);

        //Assert
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>().GetOfferNameProviderCompanyUserAsync(appId, _iamUser.UserEntityId, OfferTypeId.APP)).MustHaveHappened();
        foreach(var item in appAssignedRoleDesc)
        {
            A.CallTo(() => _userRolesRepository.CreateAppUserRole(A<Guid>._, A<string>.That.IsEqualTo(item.role))).MustHaveHappened();
            foreach (var indexItem in item.descriptions)
            {
                A.CallTo(() => _userRolesRepository.CreateAppUserRoleDescription(A<Guid>._, A<string>.That.IsEqualTo(indexItem.languageCode), A<string>.That.IsEqualTo(indexItem.description))).MustHaveHappened();
            }
        }
        A.CallTo(() => _notificationService.CreateNotifications(A<IDictionary<string, IEnumerable<string>>>._, A<Guid>._, A<IEnumerable<(string? content, NotificationTypeId notificationTypeId)>>._, A<Guid>._)).MustHaveHappened();
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IEnumerable<AppRoleData>>(result);
    }

    [Fact]
    public async Task AddActiveAppUserRoleAsync_WithCompanyUserIdNotSet_ThrowsForbiddenException()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var appName = _fixture.Create<string>();

        var appUserRoleDescription = new AppUserRoleDescription[] {
            new("de","this is test1"),
            new("en","this is test2"),
        };
        var appAssignedRoleDesc = new AppUserRole[] { new("Legal Admin", appUserRoleDescription) };
       
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>().GetOfferNameProviderCompanyUserAsync(appId, _iamUser.UserEntityId, OfferTypeId.APP))
            .ReturnsLazily(() => (true, appName, Guid.Empty, null));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(_settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.AddActiveAppUserRoleAsync(appId, appAssignedRoleDesc, _iamUser.UserEntityId).ConfigureAwait(false);

        //Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be($"user {_iamUser.UserEntityId} is not a member of the providercompany of app {appId}");
    }

    [Fact]
    public async Task AddActiveAppUserRoleAsync_WithProviderCompanyNotSet_ThrowsConflictException()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var appName = "app name";

        var appUserRoleDescription = new AppUserRoleDescription[] {
            new("de","this is test1"),
            new("en","this is test2"),
        };
        var appAssignedRoleDesc = new AppUserRole[] { new("Legal Admin", appUserRoleDescription) };
       
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>().GetOfferNameProviderCompanyUserAsync(appId, _iamUser.UserEntityId, OfferTypeId.APP))
            .ReturnsLazily(() => (true, appName, _companyUser.Id, null));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(_settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.AddActiveAppUserRoleAsync(appId, appAssignedRoleDesc, _iamUser.UserEntityId).ConfigureAwait(false);

        //Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"App {appId} providing company is not yet set.");
    }

    #endregion

    #region SubmitAppReleaseRequestAsync

    [Fact]
    public async Task SubmitAppReleaseRequestAsync_CallsOfferService()
    {
        // Arrange
        var sut = new AppReleaseBusinessLogic(null!, _options, _offerService, null!);

        // Act
        await sut.SubmitAppReleaseRequestAsync(_existingAppId, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => 
                _offerService.SubmitOfferAsync(
                    _existingAppId,
                    _iamUser.UserEntityId,
                    OfferTypeId.APP,
                    A<IEnumerable<NotificationTypeId>>._,
                    A<IDictionary<string, IEnumerable<string>>>._))
            .MustHaveHappenedOnceExactly();
    }

    #endregion

    #region SubmitOfferConsentAsync
    
    [Fact]
    public async Task SubmitOfferConsentAsync_WithEmptyAppId_ThrowsControllerArgumentException()
    {
        // Arrange
        var sut = new AppReleaseBusinessLogic(null!, _options, _offerService, null!);

        // Act
        async Task Act() => await sut.SubmitOfferConsentAsync(Guid.Empty, _fixture.Create<OfferAgreementConsent>(), _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act).ConfigureAwait(false);
        ex.Message.Should().Be("AppId must not be empty");
    }

    [Fact]
    public async Task SubmitOfferConsentAsync_WithAppId_CallsOfferService()
    {
        // Arrange
        var data = _fixture.Create<OfferAgreementConsent>();
        var sut = new AppReleaseBusinessLogic(null!, _options, _offerService, null!);

        // Act
        await sut.SubmitOfferConsentAsync(_existingAppId, data, _iamUser.UserEntityId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _offerService.CreateOrUpdateProviderOfferAgreementConsent(_existingAppId, data, _iamUser.UserEntityId, OfferTypeId.APP)).MustHaveHappenedOnceExactly();
    }

    #endregion

    #region GetAllInReviewStatusApps

    [Fact]
    public async Task GetAllInReviewStatusAppsAsync_DefaultRequest()
    {
        // Arrange
        var offerStatus = new[] { OfferStatusId.ACTIVE , OfferStatusId.IN_REVIEW };
        var InReviewData = new[] {
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.IN_REVIEW),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.IN_REVIEW),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.ACTIVE),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.ACTIVE),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.ACTIVE)
        };
        var paginationResult = (int skip, int take) => Task.FromResult(new Pagination.Source<InReviewAppData>(5, InReviewData.Skip(skip).Take(take)));
        A.CallTo(() => _offerRepository.GetAllInReviewStatusAppsAsync(A<IEnumerable<OfferStatusId>>._,A<OfferSorting>._))
            .Returns(paginationResult);
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(_settings), _offerService, _notificationService);

        // Act
        var result = await sut.GetAllInReviewStatusAppsAsync(0, 5, OfferSorting.DateAsc, null).ConfigureAwait(false);
        
        // Assert
        A.CallTo(() => _offerRepository.GetAllInReviewStatusAppsAsync(A<IEnumerable<OfferStatusId>>
            .That.Matches(x => x.Count() == 2 && x.All(y => offerStatus.Contains(y))),A<OfferSorting>._)).MustHaveHappenedOnceExactly();
        Assert.IsType<Pagination.Response<InReviewAppData>>(result);
        result.Content.Should().HaveCount(5);
        result.Content.Should().Contain(x => x.Status == OfferStatusId.ACTIVE);
        result.Content.Should().Contain(x => x.Status == OfferStatusId.IN_REVIEW);
    }

    [Fact]
    public async Task GetAllInReviewStatusAppsAsync_InReviewRequest()
    { 
        // Arrange
        var offerStatus = new[] { OfferStatusId.IN_REVIEW };
        var InReviewData = new[]{
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.IN_REVIEW),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.IN_REVIEW),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.IN_REVIEW),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.IN_REVIEW),
            new InReviewAppData(Guid.NewGuid(),null,null!, OfferStatusId.IN_REVIEW)
        };
        var paginationResult = (int skip, int take) => Task.FromResult(new Pagination.Source<InReviewAppData>(5, InReviewData.Skip(skip).Take(take)));
        A.CallTo(() => _offerRepository.GetAllInReviewStatusAppsAsync(A<IEnumerable<OfferStatusId>>._,A<OfferSorting>._))
            .Returns(paginationResult);
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(_settings), _offerService, _notificationService);

        // Act
        var result = await sut.GetAllInReviewStatusAppsAsync(0, 5, OfferSorting.DateAsc, OfferStatusIdFilter.InReview).ConfigureAwait(false);
        
        // Assert
        A.CallTo(() => _offerRepository.GetAllInReviewStatusAppsAsync(A<IEnumerable<OfferStatusId>>
            .That.Matches(x => x.Count() == 1 && x.All(y => offerStatus.Contains(y))),A<OfferSorting>._)).MustHaveHappenedOnceExactly();
        Assert.IsType<Pagination.Response<InReviewAppData>>(result);
        result.Content.Should().HaveCount(5);
        result.Content.Should().NotContain(x => x.Status == OfferStatusId.ACTIVE);
        result.Content.Should().Contain(x => x.Status == OfferStatusId.IN_REVIEW);
    }

    #endregion

    #region DeclineAppRequest
    
    [Fact]
    public async Task DeclineAppRequestAsync_CallsExpected()
    {
        // Arrange
        string IamUserId = "3e8343f7-4fe5-4296-8312-f33aa6dbde5d";
        var appId = _fixture.Create<Guid>();
        var data = new OfferDeclineRequest("Just a test");
        var settings = new AppsSettings
        {
            ServiceManagerRoles = _fixture.Create<Dictionary<string, IEnumerable<string>>>(),
            BasePortalAddress = "test"
        };
        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(_settings), _offerService, _notificationService);
     
        // Act
        await sut.DeclineAppRequestAsync(appId, IamUserId, data).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _offerService.DeclineOfferAsync(appId, IamUserId, data,
            OfferTypeId.APP, NotificationTypeId.APP_RELEASE_REJECTION,
            A<IDictionary<string, IEnumerable<string>>>._, A<string>._)).MustHaveHappenedOnceExactly();
    }

    #endregion
    
    #region DeleteAppDocument
    [Fact]
    public async Task DeleteAppDocumentsAsync_ReturnsExpectedResult()
    {
        //Arrange
        var appId = Guid.NewGuid();
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (new [] { new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.CREATED, appId, true) }, true, DocumentStatusId.PENDING, true));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _offerRepository.RemoveOfferAssignedDocument(appId, ValidDocumentId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _documentRepository.RemoveDocument(ValidDocumentId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task DeleteAppDocumentsAsync_WithNoDocument_ThrowsNotFoundException()
    {
        //Arrange
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => new ValueTuple<IEnumerable<(OfferStatusId OfferStatusId, Guid OfferId, bool IsOfferType)>, bool, DocumentStatusId, bool>());

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be($"Document {ValidDocumentId} does not exist");
    }

    [Fact]
    public async Task DeleteAppDocumentsAsync_WithNoAssignedOfferDocument_ThrowsConflictException()
    {
        //Arrange
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (new [] { new ValueTuple<OfferStatusId, Guid, bool>() }, true, DocumentStatusId.PENDING, true));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"Document {ValidDocumentId} is not assigned to an app");
    }
    
    [Fact]
    public async Task DeleteAppDocumentsAsync_WithMultipleDocumentsAssigned_ThrowsConflictException()
    {
        //Arrange
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (
                new []
                {
                    new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.CREATED, Guid.NewGuid(), true), 
                    new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.CREATED, Guid.NewGuid(), true)
                }, 
                true, 
                DocumentStatusId.PENDING, 
                true));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"Document {ValidDocumentId} is assigned to more than one app");
    }

    [Fact]
    public async Task DeleteAppDocumentsAsync_WithDocumentAssignedToService_ThrowsConflictException()
    {
        //Arrange
        var appId = Guid.NewGuid();
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (new [] { new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.CREATED, appId, false) }, true, DocumentStatusId.PENDING, true));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"Document {ValidDocumentId} is not assigned to an app");
    }

    [Fact]
    public async Task DeleteAppDocumentsAsync_WithInvalidProviderCompanyUser_ThrowsForbiddenException()
    {
        //Arrange
        var appId = Guid.NewGuid();
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (new [] { new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.CREATED, appId, true) }, true, DocumentStatusId.PENDING, false));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be($"user {IamUserId} is not a member of the same company of document {ValidDocumentId}");
    }

    [Fact]
    public async Task DeleteAppDocumentsAsync_WithInvalidOfferStatus_ThrowsConflictException()
    {
        //Arrange
        var appId = Guid.NewGuid();
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (new [] { new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.ACTIVE, appId, true) }, true, DocumentStatusId.PENDING, true));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"App {appId} is in locked state");
    }

    [Fact]
    public async Task DeleteAppDocumentsAsync_WithInvalidDocumentType_ThrowsArgumentException()
    {
        //Arrange
        var appId = Guid.NewGuid();
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.COMMERCIAL_REGISTER_EXTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (new [] { new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.CREATED, appId, true) }, false, DocumentStatusId.PENDING, true));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be($"Document {ValidDocumentId} can not get retrieved. Document type not supported");
    }

    [Fact]
    public async Task DeleteAppDocumentsAsync_WithInvalidDocumentStatus_ThrowsConflictException()
    {
        //Arrange
        var appId = Guid.NewGuid();
        var settings = new AppsSettings
        {
            DeleteDocumentTypeIds = new[] { DocumentTypeId.APP_CONTRACT }
        };
        A.CallTo(() => _documentRepository.GetAppDocumentsAsync(ValidDocumentId, IamUserId, settings.DeleteDocumentTypeIds, OfferTypeId.APP))
            .ReturnsLazily(() => (new [] { new ValueTuple<OfferStatusId, Guid, bool>(OfferStatusId.CREATED, appId, true) }, true, DocumentStatusId.LOCKED, true));

        var sut = new AppReleaseBusinessLogic(_portalRepositories, Options.Create(settings), _offerService, _notificationService);

        //Act
        async Task Act() => await sut.DeleteAppDocumentsAsync(ValidDocumentId, IamUserId).ConfigureAwait(false);

        // Assert 
        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"Document in State {DocumentStatusId.LOCKED} can't be updated");
    }
    
    #endregion

    #region GetInReviewAppDetailsById

    [Fact]
    public async Task GetinReviewAppDetailsByIdAsync_CallsExpected()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var data = _fixture.Create<InReviewOfferData>();
        A.CallTo(() => _offerRepository.GetInReviewAppDataByIdAsync(appId,OfferTypeId.APP))
            .ReturnsLazily(() => data);

        var sut = new AppReleaseBusinessLogic(_portalRepositories, _options, _offerService, null!);

        // Act
        var result = await sut.GetInReviewAppDetailsByIdAsync(appId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _offerRepository.GetInReviewAppDataByIdAsync(appId, OfferTypeId.APP)).MustHaveHappened();
        result.Should().NotBeNull();
        result.Id.Should().Be(data.id);
    }

    [Fact]
    public async Task GetinReviewAppDetailsByIdAsync_ThrowsNotFoundException()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        A.CallTo(() => _offerRepository.GetInReviewAppDataByIdAsync(appId,OfferTypeId.APP))
            .ReturnsLazily(() => (InReviewOfferData?)null);

        var sut = new AppReleaseBusinessLogic(_portalRepositories, _options, _offerService, null!);

        //Act
        async Task Act() => await sut.GetInReviewAppDetailsByIdAsync(appId).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act).ConfigureAwait(false);
        ex.Message.Should().Be($"App {appId} not found or Incorrect Status");
    }

    #endregion

    #region Setup

    private void SetupUpdateApp()
    {
        A.CallTo(() => _languageRepository.GetLanguageCodesUntrackedAsync(A<IEnumerable<string>>._))
            .Returns(new List<string>() {"de", "en"}.ToAsyncEnumerable());
        A.CallTo(() => _offerRepository.GetAppUpdateData(_notExistingAppId, _iamUser.UserEntityId, A<IEnumerable<string>>._))
            .ReturnsLazily(() => (AppUpdateData?)null);
        A.CallTo(() => _offerRepository.GetAppUpdateData(_activeAppId, _iamUser.UserEntityId, A<IEnumerable<string>>._))
            .ReturnsLazily(() => new AppUpdateData(OfferStatusId.ACTIVE, false, Array.Empty<OfferDescriptionData>(), Array.Empty<(string Shortname, bool IsMatch)>(), Array.Empty<Guid>(), new ValueTuple<Guid, string, bool>(), null, Array.Empty<PrivacyPolicyId>()));
        A.CallTo(() => _offerRepository.GetAppUpdateData(_differentCompanyAppId, _iamUser.UserEntityId, A<IEnumerable<string>>._))
            .ReturnsLazily(() => new AppUpdateData(OfferStatusId.CREATED, false, Array.Empty<OfferDescriptionData>(), Array.Empty<(string Shortname, bool IsMatch)>(), Array.Empty<Guid>(), new ValueTuple<Guid, string, bool>(), null, Array.Empty<PrivacyPolicyId>()));
        A.CallTo(() => _offerRepository.GetAppUpdateData(_existingAppId, _iamUser.UserEntityId, A<IEnumerable<string>>._))
            .ReturnsLazily(() => new AppUpdateData(OfferStatusId.CREATED, true, Array.Empty<OfferDescriptionData>(), Array.Empty<(string Shortname, bool IsMatch)>(), Array.Empty<Guid>(), new ValueTuple<Guid, string, bool>(Guid.NewGuid(), "123", false), null, Array.Empty<PrivacyPolicyId>()));
        A.CallTo(() => _offerService.ValidateSalesManager(A<Guid>._, A<string>._, A<IDictionary<string, IEnumerable<string>>>._)).Returns(_companyUser.CompanyId);
        
        A.CallTo(() => _portalRepositories.GetInstance<ILanguageRepository>()).Returns(_languageRepository);
    }

    #endregion

}
