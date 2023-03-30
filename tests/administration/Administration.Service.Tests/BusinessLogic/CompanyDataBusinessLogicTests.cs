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

using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic.Tests;

public class CompanyDataBusinessLogicTests
{
    private static readonly string IamUserId = Guid.NewGuid().ToString();
    private readonly IFixture _fixture;
    private readonly ICompanyRepository _companyRepository;
    private IPortalRepositories _portalRepositories;
    private IConsentRepository _consentRepository;
    private ICompanyRolesRepository _companyRolesRepository;
    private readonly CompanyDataBusinessLogic _sut;

    public CompanyDataBusinessLogicTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _companyRepository = A.Fake<ICompanyRepository>();
        _portalRepositories = A.Fake<IPortalRepositories>();
        _consentRepository = A.Fake<IConsentRepository>();
        _companyRolesRepository = A.Fake<ICompanyRolesRepository>();

        A.CallTo(() => _portalRepositories.GetInstance<ICompanyRepository>()).Returns(_companyRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IConsentRepository>()).Returns(_consentRepository);
        A.CallTo(() => _portalRepositories.GetInstance<ICompanyRolesRepository>()).Returns(_companyRolesRepository);
        _sut = new CompanyDataBusinessLogic(_portalRepositories);
    }

    #region GetOwnCompanyDetails

    [Fact]
    public async Task GetOwnCompanyDetailsAsync_ExpectedResults()
    {
        // Arrange
        var companyAddressDetailData = _fixture.Create<CompanyAddressDetailData>();
        A.CallTo(() => _companyRepository.GetOwnCompanyDetailsAsync(IamUserId))
            .ReturnsLazily(() => companyAddressDetailData);
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);
        
        // Act
        var result = await sut.GetOwnCompanyDetailsAsync(IamUserId);

        // Assert
        result.Should().NotBeNull();
        result.CompanyId.Should().Be(companyAddressDetailData.CompanyId);
    }

    [Fact]
    public async Task GetOwnCompanyDetailsAsync_ThrowsConflictException()
    {
        // Arrange
        A.CallTo(() => _companyRepository.GetOwnCompanyDetailsAsync(IamUserId))
            .ReturnsLazily(() => (CompanyAddressDetailData?)null);
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);
        
        // Act
        async Task Act() => await sut.GetOwnCompanyDetailsAsync(IamUserId);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"user {IamUserId} is not associated with any company");
    }

    #endregion

    #region GetCompanyRoleAndConsentAgreementDetails

    [Fact]
    public async Task GetCompanyRoleAndConsentAgreementDetails_CallsExpected()
    {
        // Arrange
        var companyRoleConsentDatas = _fixture.CreateMany<CompanyRoleConsentData>(2).ToAsyncEnumerable();
        A.CallTo(() => _companyRepository.GetCompanyRoleAndConsentAgreementDetailsAsync(IamUserId))
            .ReturnsLazily(() => companyRoleConsentDatas);

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        var result = await sut.GetCompanyRoleAndConsentAgreementDetailsAsync(IamUserId).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        A.CallTo(() => _companyRepository.GetCompanyRoleAndConsentAgreementDetailsAsync(A<string>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetCompanyRoleAndConsentAgreementDetails_ThrowsConflictException()
    {
        // Arrange
        var companyRoleConsentDatas = _fixture.CreateMany<CompanyRoleConsentData>(0).ToAsyncEnumerable();
        A.CallTo(() => _companyRepository.GetCompanyRoleAndConsentAgreementDetailsAsync(IamUserId))
            .Returns(companyRoleConsentDatas);

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.GetCompanyRoleAndConsentAgreementDetailsAsync(IamUserId).ToListAsync().ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"user {IamUserId} is not associated with any company or Incorrect Status");
    }

    #endregion

    #region  CreateCompanyRoleAndConsentAgreementDetails

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_ReturnsExpected()
    {
        // Arrange
        var companyId = _fixture.Create<Guid>();
        var companyUserId = _fixture.Create<Guid>();
        var agreementId1 = _fixture.Create<Guid>();
        var agreementId2 = _fixture.Create<Guid>();
        var companyRole = new [] { CompanyRoleId.OPERATOR, CompanyRoleId.SERVICE_PROVIDER};

        var consentStatusDetails = new []{
            new ConsentStatusDetails(_fixture.Create<Guid>(),agreementId1,ConsentStatusId.INACTIVE)
        };
        var companyRoleConsentDetails = new []{
            new CompanyRoleConsentDetails( CompanyRoleId.ACTIVE_PARTICIPANT,
                new []{new ConsentDetails(agreementId1, ConsentStatusId.ACTIVE)}),
            
            new CompanyRoleConsentDetails( CompanyRoleId.APP_PROVIDER,
                new []{new ConsentDetails(agreementId2, ConsentStatusId.ACTIVE)})
        };
        var data = companyRoleConsentDetails.Select(x => x.CompanyRole);

        var agreementData = new (Guid, CompanyRoleId)[]{
            new (agreementId1, CompanyRoleId.ACTIVE_PARTICIPANT),
            new (agreementId2, CompanyRoleId.APP_PROVIDER),
            new (_fixture.Create<Guid>(), CompanyRoleId.APP_PROVIDER),
            new (_fixture.Create<Guid>(), CompanyRoleId.APP_PROVIDER),
            new (_fixture.Create<Guid>(), CompanyRoleId.SERVICE_PROVIDER),
            new (_fixture.Create<Guid>(), CompanyRoleId.SERVICE_PROVIDER),
            new (agreementId1, CompanyRoleId.OPERATOR)

        }.ToAsyncEnumerable();

        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId, A<IEnumerable<CompanyRoleId>>._))
            .Returns((true, companyId, companyRole, companyUserId, consentStatusDetails));
        
        A.CallTo(() => _companyRepository.GetAgreementAssignedRolesDataAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(agreementData);
        
        A.CallTo(() => _consentRepository.AddAttachAndModifyConsents(A<IEnumerable<ConsentStatusDetails>>._,A<IEnumerable<(Guid, ConsentStatusId)>>._,A<Guid>._,A<Guid>._,A<DateTimeOffset>._))
            .Returns(new Consent[] {
                new(_fixture.Create<Guid>(), agreementId1, companyId, companyUserId, ConsentStatusId.INACTIVE, DateTimeOffset.UtcNow),
                new(_fixture.Create<Guid>(), agreementId2, companyId, companyUserId, ConsentStatusId.ACTIVE, DateTimeOffset.UtcNow)
            });

        var sut = new CompanyDataBusinessLogic(_portalRepositories);
        
        // Act
        await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId,companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _companyRolesRepository.CreateCompanyAssignedRole(companyId, A<CompanyRoleId>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _consentRepository.AddAttachAndModifyConsents( A<IEnumerable<ConsentStatusDetails>>._,
            A<IEnumerable<(Guid, ConsentStatusId)>>._, A<Guid>._, A<Guid>._, A<DateTimeOffset>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_CompanyStatus_ThrowsConflictException()
    {
        // Arrange
        var companyRoleConsentDetails = _fixture.CreateMany<CompanyRoleConsentDetails>(2);
        var companyId = _fixture.Create<Guid>();
        var companyUserId = _fixture.Create<Guid>();

        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId, A<IEnumerable<CompanyRoleId>>._))
            .Returns((false, companyId, null, companyUserId, null));
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId, companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("Company Status is Incorrect");
    }

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_AgreementAssignedRole_ThrowsControllerArgumentException()
    {
        // Arrange
        var companyId = _fixture.Create<Guid>();
        var companyUserId = _fixture.Create<Guid>();
        var companyRole = new [] { CompanyRoleId.OPERATOR, CompanyRoleId.SERVICE_PROVIDER};
        var companyRoleConsentDetails = new []{
            new CompanyRoleConsentDetails( CompanyRoleId.ACTIVE_PARTICIPANT,
                new []{ new ConsentDetails(_fixture.Create<Guid>(), ConsentStatusId.ACTIVE) })};

        var consentStatusDetails = new []{
            new ConsentStatusDetails(_fixture.Create<Guid>(),_fixture.Create<Guid>(),ConsentStatusId.ACTIVE)
        };

        var agreementData = new (Guid, CompanyRoleId)[]{
            new (_fixture.Create<Guid>(), CompanyRoleId.SERVICE_PROVIDER),
            new (_fixture.Create<Guid>(), CompanyRoleId.OPERATOR),
        }.ToAsyncEnumerable();

        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId,A<IEnumerable<CompanyRoleId>>._))
            .Returns((true, companyId, companyRole, companyUserId, consentStatusDetails));

        A.CallTo(() => _companyRepository.GetAgreementAssignedRolesDataAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(agreementData);
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId,companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be("All agreement need to get signed");
    }

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_ConsentStatus_ThrowsConflictException()
    {
        // Arrange
        var companyId = _fixture.Create<Guid>();
        var agreementId = _fixture.Create<Guid>();
        var companyUserId = _fixture.Create<Guid>();
        var companyRole = new [] { CompanyRoleId.OPERATOR, CompanyRoleId.SERVICE_PROVIDER};
        var agreementAssignedRole = new []{ CompanyRoleId.APP_PROVIDER };
        var companyRoleConsentDetails = new []{
            new CompanyRoleConsentDetails( CompanyRoleId.APP_PROVIDER,
                new []{ new ConsentDetails(_fixture.Create<Guid>(), ConsentStatusId.INACTIVE) })};

        var consentStatusDetails = new []{
            new ConsentStatusDetails(_fixture.Create<Guid>(),_fixture.Create<Guid>(),ConsentStatusId.ACTIVE)
        };

        var agreementData = new (Guid,CompanyRoleId)[]{
            new (agreementId, CompanyRoleId.SERVICE_PROVIDER),
            new (_fixture.Create<Guid>(), CompanyRoleId.OPERATOR),
            new (agreementId, CompanyRoleId.APP_PROVIDER)
        }.ToAsyncEnumerable();

        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId,A<IEnumerable<CompanyRoleId>>._))
            .Returns((true, companyId, companyRole, companyUserId, consentStatusDetails));

        A.CallTo(() => _companyRepository.GetAgreementAssignedRolesDataAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(agreementData);
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId,companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be("All agreement need to get signed");
    }

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_CompanyRoleId_ThrowsConflictException()
    {
        // Arrange
        var companyId = _fixture.Create<Guid>();
        var companyUserId = _fixture.Create<Guid>();
        var agreementId = _fixture.Create<Guid>();
        var companyRole = new [] { CompanyRoleId.APP_PROVIDER, CompanyRoleId.ACTIVE_PARTICIPANT};
        var companyRoleConsentDetails = new []{
            new CompanyRoleConsentDetails( CompanyRoleId.APP_PROVIDER,
                new []{ new ConsentDetails(agreementId, ConsentStatusId.ACTIVE) })};

        var consentStatusDetails = new []{
            new ConsentStatusDetails(_fixture.Create<Guid>(),_fixture.Create<Guid>(),ConsentStatusId.ACTIVE)
        };
        var agreementData = new (Guid, CompanyRoleId)[]{
            new (_fixture.Create<Guid>(), CompanyRoleId.SERVICE_PROVIDER),
            new (_fixture.Create<Guid>(), CompanyRoleId.OPERATOR),
            new (agreementId, CompanyRoleId.APP_PROVIDER)
        }.ToAsyncEnumerable();

        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId,A<IEnumerable<CompanyRoleId>>._))
            .Returns((true, companyId, companyRole, companyUserId, consentStatusDetails));
        
        A.CallTo(() => _companyRepository.GetAgreementAssignedRolesDataAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(agreementData);
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId,companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("companyRole already exists");
    }

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_ThrowsNotFoundException()
    {
        // Arrange
        var companyRoleConsentDetails = _fixture.CreateMany<CompanyRoleConsentDetails>(2);
        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId,A<IEnumerable<CompanyRoleId>>._))
            .Returns(((bool,Guid,IEnumerable<CompanyRoleId>?,Guid,IEnumerable<ConsentStatusDetails>?))default);
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId,companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be($"user {IamUserId} is not associated with any company");
    }

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_ThrowsUnexpectedConditionException()
    {
        // Arrange
        var companyRoleConsentDetails = _fixture.CreateMany<CompanyRoleConsentDetails>(2);
        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId,A<IEnumerable<CompanyRoleId>>._))
            .Returns((true, Guid.NewGuid(), null!, Guid.NewGuid(), null!));
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId,companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<UnexpectedConditionException>(Act);
        ex.Message.Should().Be($"none of AgreementAssignedRoles, CompanyRoleIds, ConsentStatusDetails should ever be null here");
    }

    [Fact]
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync_AgreementAssignedrole_ThrowsUnexpectedConditionException()
    {
        // Arrange
        var companyRoleConsentDetails = new []{
            new CompanyRoleConsentDetails( CompanyRoleId.APP_PROVIDER,
                new []{ new ConsentDetails(_fixture.Create<Guid>(), ConsentStatusId.INACTIVE) })};
        var companyRole = new []{CompanyRoleId.ACTIVE_PARTICIPANT, CompanyRoleId.SERVICE_PROVIDER};
        var consentStatusDetails = new []{
            new ConsentStatusDetails(_fixture.Create<Guid>(),_fixture.Create<Guid>(),ConsentStatusId.ACTIVE)
        };
        var agreementAssignedRole = _fixture.CreateMany<(Guid,CompanyRoleId)>(0).ToAsyncEnumerable();

        A.CallTo(() => _companyRepository.GetCompanyRolesDataAsync(IamUserId,A<IEnumerable<CompanyRoleId>>._))
            .Returns((true, Guid.NewGuid(), companyRole, Guid.NewGuid(), consentStatusDetails));

        A.CallTo(() => _companyRepository.GetAgreementAssignedRolesDataAsync(A<IEnumerable<CompanyRoleId>>._))
            .Returns(agreementAssignedRole);
        
        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyRoleAndConsentAgreementDetailsAsync(IamUserId,companyRoleConsentDetails).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<UnexpectedConditionException>(Act);
        ex.Message.Should().Be($"none of AgreementAssignedRoles, CompanyRoleIds, ConsentStatusDetails should ever be null here");
    }

    #endregion

    #region CompanyAssigendUseCaseDetails

    [Fact]
    public async Task GetCompanyAssigendUseCaseDetailsAsync_ResturnsExpected()
    {
        // Arrange
        var companyAssignedUseCaseData = _fixture.CreateMany<CompanyAssignedUseCaseData>(2).ToAsyncEnumerable();
        A.CallTo(() => _companyRepository.GetCompanyAssigendUseCaseDetailsAsync(IamUserId))
            .ReturnsLazily(() => companyAssignedUseCaseData);

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        var result = await sut.GetCompanyAssigendUseCaseDetailsAsync(IamUserId).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();

        A.CallTo(() => _companyRepository.GetCompanyAssigendUseCaseDetailsAsync(A<string>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateCompanyAssignedUseCaseDetailsAsync_NoConent_ReturnsExpected()
    {
        // Arrange
        var useCaseId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();
        A.CallTo(() => _companyRepository.GetCompanyStatusAndUseCaseIdAsync(IamUserId,useCaseId))
            .Returns((false, true, companyId));

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        var result = await sut.CreateCompanyAssignedUseCaseDetailsAsync(IamUserId, useCaseId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _companyRepository.CreateCompanyAssignedUseCase(companyId, useCaseId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        result.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateCompanyAssignedUseCaseDetailsAsync_AlreadyReported_ReturnsExpected()
    {
        // Arrange
        var useCaseId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();

        A.CallTo(() => _companyRepository.GetCompanyStatusAndUseCaseIdAsync(IamUserId,useCaseId))
            .Returns((true, true, companyId));

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        var result = await sut.CreateCompanyAssignedUseCaseDetailsAsync(IamUserId, useCaseId).ConfigureAwait(false);

        // Assert
        result.Should().Be(System.Net.HttpStatusCode.AlreadyReported);
        A.CallTo(() => _companyRepository.CreateCompanyAssignedUseCase(companyId, useCaseId)).MustNotHaveHappened();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustNotHaveHappened();

    }

    [Fact]
    public async Task CreateCompanyAssignedUseCaseDetailsAsync_ThrowsConflictException()
    {
        // Arrange
        var useCaseId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();

        A.CallTo(() => _companyRepository.GetCompanyStatusAndUseCaseIdAsync(IamUserId,useCaseId))
            .Returns((false, false, companyId));

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.CreateCompanyAssignedUseCaseDetailsAsync(IamUserId, useCaseId).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("Company Status is Incorrect");
    }

    [Fact]
    public async Task RemoveCompanyAssignedUseCaseDetailsAsync_ReturnsExpected()
    {
        // Arrange
        var useCaseId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();

        A.CallTo(() => _companyRepository.GetCompanyStatusAndUseCaseIdAsync(IamUserId,useCaseId))
            .Returns((true, true, companyId));

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        await sut.RemoveCompanyAssignedUseCaseDetailsAsync(IamUserId, useCaseId).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _companyRepository.RemoveCompanyAssignedUseCase(companyId, useCaseId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RemoveCompanyAssignedUseCaseDetailsAsync_companyStatus_ThrowsConflictException()
    {
        // Arrange
        var useCaseId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();

        A.CallTo(() => _companyRepository.GetCompanyStatusAndUseCaseIdAsync(IamUserId,useCaseId))
            .Returns((true, false, companyId));

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.RemoveCompanyAssignedUseCaseDetailsAsync(IamUserId, useCaseId).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("Company Status is Incorrect");
    }

    [Fact]
    public async Task RemoveCompanyAssignedUseCaseDetailsAsync_useCaseId_ThrowsConflictException()
    {
        // Arrange
        var useCaseId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();

        A.CallTo(() => _companyRepository.GetCompanyStatusAndUseCaseIdAsync(IamUserId,useCaseId))
            .Returns((false, true, companyId));

        var sut = new CompanyDataBusinessLogic(_portalRepositories);

        // Act
        async Task Act() => await sut.RemoveCompanyAssignedUseCaseDetailsAsync(IamUserId, useCaseId).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"UseCaseId {useCaseId} is not available");
    }

    #endregion
}
