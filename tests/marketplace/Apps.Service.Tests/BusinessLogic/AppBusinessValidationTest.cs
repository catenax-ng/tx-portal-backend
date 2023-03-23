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
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Xunit;

namespace Org.Eclipse.TractusX.Portal.Backend.Apps.Service.BusinessLogic.Tests;

public class AppBusinessValidationTest
{
    private readonly string _iamUserId = Guid.NewGuid().ToString();
    private readonly IOfferRepository _offerRepository;
    private readonly IPortalRepositories _portalRepositories;
    private readonly IFixture _fixture;
    private readonly AppBusinessValidation _sut;

    public AppBusinessValidationTest()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _portalRepositories = A.Fake<IPortalRepositories>();
        _offerRepository = A.Fake<IOfferRepository>();
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>()).Returns(_offerRepository);
        _sut = new AppBusinessValidation();
    }

    [Fact]    
    public async Task ValidateAndGetAppDescription_ThrowsNotFoundException()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        A.CallTo(() => _offerRepository.GetActiveOfferDescriptionDataByIdAsync(appId, OfferTypeId.APP, _iamUserId))
            .ReturnsLazily(() => ((bool IsStatusActive, bool IsProviderCompanyUser, IEnumerable<LocalizedDescription> OfferDescriptionDatas))default);

        // Act
        async Task Act() => await _sut.ValidateAndGetAppDescription(appId, _iamUserId, _offerRepository).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be($"App {appId} does not exist.");  
    }

    [Fact]    
    public async Task ValidateAndGetAppDescription_ThrowsConflictException()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var offerDescription = _fixture.CreateMany<LocalizedDescription>(3);
        var appDescriptionData = (IsStatusActive: false, IsProviderCompanyUser: true, OfferDescriptionDatas: offerDescription);
       
        A.CallTo(() => _offerRepository.GetActiveOfferDescriptionDataByIdAsync(appId, OfferTypeId.APP, _iamUserId))
            .ReturnsLazily(() => appDescriptionData);
        
        // Act
        async Task Act() => await _sut.ValidateAndGetAppDescription(appId, _iamUserId, _offerRepository).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"App {appId} is in InCorrect Status");  
    }

    [Fact]    
    public async Task ValidateAndGetAppDescription_ThrowsForbiddenException()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var offerDescription = _fixture.CreateMany<LocalizedDescription>(3);
        var appDescriptionData = (IsStatusActive: true, IsProviderCompanyUser: false, OfferDescriptionDatas: offerDescription);
        
        A.CallTo(() => _offerRepository.GetActiveOfferDescriptionDataByIdAsync(appId, OfferTypeId.APP, _iamUserId))
            .ReturnsLazily(() => appDescriptionData);
        
        // ActValidateAndGetAppDescription
        async Task Act() => await _sut.ValidateAndGetAppDescription(appId, _iamUserId, _offerRepository).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be($"user {_iamUserId} is not a member of the providercompany of App {appId}");  
    }

    [Fact]
    public async Task ValidateAndGetAppDescription_ReturnsExpectedResult()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var offerDescription = _fixture.CreateMany<LocalizedDescription>(3);
        var appDescriptionData = (IsStatusActive: true, IsProviderCompanyUser: true, OfferDescriptionDatas: offerDescription);
        A.CallTo(() => _offerRepository.GetActiveOfferDescriptionDataByIdAsync(appId, OfferTypeId.APP, _iamUserId))
            .ReturnsLazily(() => appDescriptionData);

        // Act
        var result = await _sut.ValidateAndGetAppDescription(appId, _iamUserId, _offerRepository).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.Select(x => x.LanguageCode).Should().Contain(offerDescription.Select(od => od.LanguageCode));
    }

    [Fact]
    public async Task ValidateAndGetAppDescription_withNullDescriptionData_ThowsUnexpectedConditionException()
    {
        // Arrange
        var appId = _fixture.Create<Guid>();
        var appDescriptionData = (IsStatusActive: true, IsProviderCompanyUser: true, OfferDescriptionDatas: (IEnumerable<LocalizedDescription>?)null);
        A.CallTo(() => _offerRepository.GetActiveOfferDescriptionDataByIdAsync(appId, OfferTypeId.APP, _iamUserId))
            .ReturnsLazily(() => appDescriptionData);

        // Act
        var Act = () => _sut.ValidateAndGetAppDescription(appId, _iamUserId, _offerRepository);

        // Assert
        var result = await Assert.ThrowsAsync<UnexpectedConditionException>(Act).ConfigureAwait(false);
        result.Message.Should().Be("offerDescriptionDatas should never be null here");
    }
}
