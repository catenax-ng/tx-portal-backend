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
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Service;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Services.Service.BusinessLogic;
using Xunit;

namespace Org.Eclipse.TractusX.Portal.Backend.Services.Service.Tests.BusinessLogic;

public class ServiceReleaseBusinessLogicTest
{
    private readonly IFixture _fixture;
    private readonly IOfferRepository _offerRepository;
    private readonly IPortalRepositories _portalRepositories;
    private readonly IOfferService _offerService;
    
    public ServiceReleaseBusinessLogicTest()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _portalRepositories = A.Fake<IPortalRepositories>();
        
        _offerRepository = A.Fake<IOfferRepository>();
        
        _offerService = A.Fake<IOfferService>();

        SetupRepositories();
        
    }

    [Fact]
    public async Task GetServiceAgreementData_ReturnsExpectedResult()
    {
        //Arrange
        var data = _fixture.CreateMany<AgreementDocumentData>(5).ToAsyncEnumerable();
        var offerService = A.Fake<IOfferService>();
        _fixture.Inject(offerService);
        A.CallTo(() => offerService.GetOfferTypeAgreementsAsync(OfferTypeId.SERVICE))
            .Returns(data);

        //Act
        var sut = _fixture.Create<ServiceReleaseBusinessLogic>();
        var result = await sut.GetServiceAgreementDataAsync().ToListAsync().ConfigureAwait(false);
        
        // Assert 
        A.CallTo(() => offerService.GetOfferTypeAgreementsAsync(A<OfferTypeId>._))
            .MustHaveHappenedOnceExactly();
        result.Should().HaveCount(5);
    }

    private void SetupRepositories()
    {
        
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>()).Returns(_offerRepository);
        _fixture.Inject(_portalRepositories);
    }
}
