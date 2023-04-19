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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Daps.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.HttpClientExtensions;
using Org.Eclipse.TractusX.Portal.Backend.Framework.IO;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Token;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Extensions;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Org.Eclipse.TractusX.Portal.Backend.Daps.Library;

public class DapsService : IDapsService
{
    private const string BaseSecurityProfile = "BASE_SECURITY_PROFILE";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ITokenService _tokenService;
    private readonly DapsSettings _settings;

    /// <summary>
    /// Creates a new instance of <see cref="DapsService"/>
    /// </summary>
    /// <param name="tokenService"></param>
    /// <param name="options"></param>
    public DapsService(ITokenService tokenService, IOptions<DapsSettings> options)
    {
        _tokenService = tokenService;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public Task<DapsResponse?> EnableDapsAuthAsync(string clientName, string connectorUrl, string businessPartnerNumber, IFormFile formFile, CancellationToken cancellationToken)
    {
        connectorUrl.EnsureValidHttpUrl(() => nameof(connectorUrl));
        return HandleRequest(clientName, connectorUrl, businessPartnerNumber, formFile, cancellationToken);
    }

    private async Task<DapsResponse?> HandleRequest(string clientName, string connectorUrl, string businessPartnerNumber,
        IFormFile formFile, CancellationToken cancellationToken)
    {
        var httpClient = await _tokenService.GetAuthorizedClient<DapsService>(_settings, cancellationToken).ConfigureAwait(false);

        using var stream = formFile.OpenReadStream();

        var multiPartStream = new MultipartFormDataContent();
        multiPartStream.Add(new StreamContent(stream), "file", formFile.FileName);
        multiPartStream.Add(new StringContent(clientName), "clientName");
        multiPartStream.Add(new StringContent(BaseSecurityProfile), "securityProfile");
        multiPartStream.Add(new StringContent(connectorUrl.AppendToPathEncoded(businessPartnerNumber)), "referringConnector");

        var result = await httpClient.PostAsync(string.Empty, multiPartStream, cancellationToken)
            .CatchingIntoServiceExceptionFor("daps-post", HttpAsyncResponseMessageExtension.RecoverOptions.INFRASTRUCTURE).ConfigureAwait(false);
        return await result.Content.ReadFromJsonAsync<DapsResponse>(Options, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task EnableDapsAuthAsync(string clientName, string connectorUrl, string businessPartnerNumber, string fileName, byte[] content, MediaTypeId mediaTypeId, CancellationToken cancellationToken)
    {
        connectorUrl.EnsureValidHttpUrl(() => nameof(connectorUrl));
        return HandleRequest(clientName, connectorUrl, businessPartnerNumber, fileName, content, mediaTypeId, cancellationToken);
    }

    private async Task HandleRequest(string clientName, string connectorUrl, string businessPartnerNumber, string fileName, byte[] content, MediaTypeId mediaTypeId, CancellationToken cancellationToken)
    {
        var httpClient = await _tokenService.GetAuthorizedClient<DapsService>(_settings, cancellationToken).ConfigureAwait(false);

        using var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaTypeId.MapToMediaType());

        using var multiPart = new MultipartFormDataContent();
        multiPart.Add(fileContent, "file", fileName);
        multiPart.Add(new StringContent(clientName), "clientName");
        multiPart.Add(new StringContent(BaseSecurityProfile), "securityProfile");
        multiPart.Add(new StringContent(connectorUrl.AppendToPathEncoded(businessPartnerNumber)), "referringConnector");

        await httpClient.PostAsync(string.Empty, multiPart, cancellationToken)
            .CatchingIntoServiceExceptionFor("daps-post", HttpAsyncResponseMessageExtension.RecoverOptions.INFRASTRUCTURE).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDapsClient(string dapsClientId, CancellationToken cancellationToken)
    {
        var httpClient = await _tokenService.GetAuthorizedClient<DapsService>(_settings, cancellationToken).ConfigureAwait(false);
        await httpClient.DeleteAsync(dapsClientId, cancellationToken)
            .CatchingIntoServiceExceptionFor("daps-delete", HttpAsyncResponseMessageExtension.RecoverOptions.INFRASTRUCTURE).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateDapsConnectorUrl(string dapsClientId, string connectorUrl, string businessPartnerNumber,
        CancellationToken cancellationToken)
    {
        var dapsUpdate = new DapsUpdateData(connectorUrl.AppendToPathEncoded(businessPartnerNumber));
        var httpClient = await _tokenService.GetAuthorizedClient<DapsService>(_settings, cancellationToken).ConfigureAwait(false);
        await httpClient.PutAsJsonAsync(dapsClientId, dapsUpdate, cancellationToken)
            .CatchingIntoServiceExceptionFor("daps-update", HttpAsyncResponseMessageExtension.RecoverOptions.INFRASTRUCTURE).ConfigureAwait(false);

        return true;
    }
}
