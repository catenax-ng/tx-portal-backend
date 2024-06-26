/********************************************************************************
 * Copyright (c) 2024 Contributors to the Eclipse Foundation
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

using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models.Configuration;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Tests.Shared;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Token;
using Org.Eclipse.TractusX.Portal.Backend.IssuerComponent.Library.DependencyInjection;
using Org.Eclipse.TractusX.Portal.Backend.IssuerComponent.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.IssuerComponent.Library.Service;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared.Extensions;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Org.Eclipse.TractusX.Portal.Backend.IssuerComponent.Library.Tests;

public class IssuerComponentServiceTests
{
    #region Initialization

    private const string Token = "test123";

    private readonly ITokenService _tokenService;
    private readonly IOptions<IssuerComponentSettings> _options;
    private readonly IFixture _fixture;

    public IssuerComponentServiceTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _options = Options.Create(new IssuerComponentSettings
        {
            Password = "passWord",
            Scope = "test",
            Username = "user@name",
            BaseAddress = "https://base.address.com",
            ClientId = "CatenaX",
            ClientSecret = "pass@Secret",
            GrantType = "cred",
            TokenAddress = "https://key.cloak.com",
            CallbackBaseUrl = "https://example.org/callback",
            EncryptionConfigIndex = 0,
            EncryptionConfigs = new EncryptionModeConfig[]
            {
                new()
                {
                    Index = 1,
                    EncryptionKey = "5892b7e151628aed2a6abf715892b7e151628aed2a62b7e151628aed2a6abf71",
                    CipherMode = CipherMode.CBC,
                    PaddingMode = PaddingMode.PKCS7
                },
            }
        });
        _tokenService = A.Fake<ITokenService>();
        _fixture.Inject(_options);
    }

    #endregion

    #region CreateBpnlCredential

    [Fact]
    public async Task CreateBpnlCredential_WithValidData_DoesNotThrowException()
    {
        // Arrange
        var data = _fixture.Create<CreateBpnCredentialRequest>();
        _fixture.ConfigureTokenServiceFixture<IssuerComponentService>(new HttpResponseMessage(HttpStatusCode.OK));
        var sut = _fixture.Create<IssuerComponentService>();

        // Act
        var result = await sut.CreateBpnlCredential(data, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBpnlCredential_WithInvalidData_ThrowsServiceException()
    {
        // Arrange
        var data = _fixture.Create<CreateBpnCredentialRequest>();
        _fixture.ConfigureTokenServiceFixture<IssuerComponentService>(new HttpResponseMessage(HttpStatusCode.BadRequest));
        var sut = _fixture.Create<IssuerComponentService>();

        // Act
        async Task Act() => await sut.CreateBpnlCredential(data, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ServiceException>(Act);
        ex.Message.Should().Be("call to external system issuer-component-bpn-post failed with statuscode 400");
        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBpnlCredential_WitException_ThrowsServiceException()
    {
        // Arrange
        var data = _fixture.Create<CreateBpnCredentialRequest>();
        var error = new Exception("random exception");
        var httpMessageHandlerMock = new HttpMessageHandlerMock(HttpStatusCode.BadRequest, null, error);
        using var httpClient = new HttpClient(httpMessageHandlerMock);
        httpClient.BaseAddress = new Uri("https://base.address.com");
        A.CallTo(() => _tokenService.GetAuthorizedClient<IssuerComponentService>(_options.Value, A<CancellationToken>._)).Returns(httpClient);
        var sut = new IssuerComponentService(_tokenService, null!, _options);

        // Act
        async Task Act() => await sut.CreateBpnlCredential(data, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ServiceException>(Act);
        ex.Message.Should().Be("call to external system issuer-component-bpn-post failed");
        ex.InnerException.Should().Be(error);
    }

    #endregion

    #region CreateMembershipCredential

    [Fact]
    public async Task CreateMembershipCredential_WithValidData_DoesNotThrowException()
    {
        // Arrange
        var data = _fixture.Create<CreateMembershipCredentialRequest>();
        _fixture.ConfigureTokenServiceFixture<IssuerComponentService>(new HttpResponseMessage(HttpStatusCode.OK));
        var sut = _fixture.Create<IssuerComponentService>();

        // Act
        var result = await sut.CreateMembershipCredential(data, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMembershipCredential_WithInvalidData_ThrowsServiceException()
    {
        // Arrange
        var data = _fixture.Create<CreateMembershipCredentialRequest>();
        _fixture.ConfigureTokenServiceFixture<IssuerComponentService>(new HttpResponseMessage(HttpStatusCode.BadRequest));
        var sut = _fixture.Create<IssuerComponentService>();

        // Act
        async Task Act() => await sut.CreateMembershipCredential(data, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ServiceException>(Act);
        ex.Message.Should().Be("call to external system issuer-component-membership-post failed with statuscode 400");
        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMembershipCredential_WitException_ThrowsServiceException()
    {
        // Arrange
        var data = _fixture.Create<CreateMembershipCredentialRequest>();
        var error = new Exception("random exception");
        var httpMessageHandlerMock = new HttpMessageHandlerMock(HttpStatusCode.BadRequest, null, error);
        using var httpClient = new HttpClient(httpMessageHandlerMock);
        httpClient.BaseAddress = new Uri("https://base.address.com");
        A.CallTo(() => _tokenService.GetAuthorizedClient<IssuerComponentService>(_options.Value, A<CancellationToken>._)).Returns(httpClient);
        var sut = new IssuerComponentService(_tokenService, null!, _options);

        // Act
        async Task Act() => await sut.CreateMembershipCredential(data, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ServiceException>(Act);
        ex.Message.Should().Be("call to external system issuer-component-membership-post failed");
        ex.InnerException.Should().Be(error);
    }

    #endregion

    #region CreateFrameworkCredential

    [Fact]
    public async Task CreateFrameworkCredential_WithValidData_DoesNotThrowException()
    {
        // Arrange
        var credentialId = Guid.NewGuid();
        var data = _fixture.Create<CreateFrameworkCredentialRequest>();
        HttpRequestMessage? request = null;
        _fixture.ConfigureHttpClientFactoryFixture(
            nameof(IssuerComponentService),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(credentialId.ToString())) },
            requestMessage => request = requestMessage);
        var sut = _fixture.Create<IssuerComponentService>();

        // Act
        var result = await sut.CreateFrameworkCredential(data, Token, CancellationToken.None);

        // Assert
        result.Should().Be(credentialId);
        request.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateFrameworkCredential_WithInvalidData_ThrowsServiceException()
    {
        // Arrange
        var data = _fixture.Create<CreateFrameworkCredentialRequest>();
        _fixture.ConfigureHttpClientFactoryFixture(nameof(IssuerComponentService), new HttpResponseMessage(HttpStatusCode.BadRequest));
        var sut = _fixture.Create<IssuerComponentService>();

        // Act
        async Task Act() => await sut.CreateFrameworkCredential(data, Token, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ServiceException>(Act);
        ex.Message.Should().Be("call to external system issuer-component-framework-post failed with statuscode 400");
        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateFrameworkCredential_WitException_ThrowsServiceException()
    {
        // Arrange
        var data = _fixture.Create<CreateFrameworkCredentialRequest>();
        var error = new Exception("random exception");
        var httpClientFactory = _fixture.Create<IHttpClientFactory>();
        var httpMessageHandlerMock = new HttpMessageHandlerMock(HttpStatusCode.BadRequest, null, error);
        using var httpClient = new HttpClient(httpMessageHandlerMock);
        httpClient.BaseAddress = new Uri("https://base.address.com");
        A.CallTo(() => httpClientFactory.CreateClient(nameof(IssuerComponentService))).Returns(httpClient);
        var sut = new IssuerComponentService(null!, httpClientFactory, _options);

        // Act
        async Task Act() => await sut.CreateFrameworkCredential(data, Token, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ServiceException>(Act);
        ex.Message.Should().Be("call to external system issuer-component-framework-post failed");
        ex.InnerException.Should().Be(error);
    }

    #endregion
}
