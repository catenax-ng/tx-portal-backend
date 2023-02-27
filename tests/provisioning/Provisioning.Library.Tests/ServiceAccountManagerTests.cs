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
using Org.Eclipse.TractusX.Portal.Backend.Keycloak.Factory;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.DBAccess;
using Xunit;

namespace Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Tests;

public class ServiceAccountManagerTests
{
    private readonly IKeycloakFactory _factory;
    private readonly IProvisioningDBAccess _provisioningDbAccess;
    private readonly ProvisioningSettings _settings;
    private readonly ProvisioningManager _sut;

    public ServiceAccountManagerTests()
    {
        var fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _provisioningDbAccess = A.Fake<IProvisioningDBAccess>();
        _factory = A.Fake<IKeycloakFactory>();
        _settings = new ProvisioningSettings
        {
            ServiceAccountClientPrefix = "sa"
        };
        
        _sut = new ProvisioningManager(_factory, _provisioningDbAccess, Options.Create(_settings));
    }

    [Fact]
    public async Task GetNextServiceAccountClientIdWithIdAsync()
    {
        // Arrange
        SetupGetNextServiceAccountClient();
        
        // Act
        var result = await _sut.GetNextServiceAccountClientIdWithIdAsync().ConfigureAwait(false);
        
        // Assert
        result.id.Should().Be("1");
        result.clientId.Should().Be("sa1");
    }

    #region Setup

    private void SetupGetNextServiceAccountClient()
    {
        A.CallTo(() => _provisioningDbAccess.GetNextClientSequenceAsync()).ReturnsLazily(() => 1);
    }

    #endregion
}