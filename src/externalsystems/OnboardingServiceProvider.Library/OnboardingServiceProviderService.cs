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

using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Framework.HttpClientExtensions;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Token;
using Org.Eclipse.TractusX.Portal.Backend.OnboardingServiceProvider.Library.DependencyInjection;
using Org.Eclipse.TractusX.Portal.Backend.OnboardingServiceProvider.Library.Models;
using System.Net.Http.Json;

namespace Org.Eclipse.TractusX.Portal.Backend.OnboardingServiceProvider.Library;

public class OnboardingServiceProviderService : IOnboardingServiceProviderService
{
    private readonly ITokenService _tokenService;
    private readonly OnboardingServiceProviderSettings _settings;

    /// <summary>
    /// Creates a new instance of <see cref="OnboardingServiceProviderService"/>
    /// </summary>
    /// <param name="tokenService"></param>
    /// <param name="options"></param>
    public OnboardingServiceProviderService(ITokenService tokenService, IOptions<OnboardingServiceProviderSettings> options)
    {
        _tokenService = tokenService;
        _settings = options.Value;
    }

    public async Task<bool> TriggerProviderCallback(string callbackUrl, OnboardingServiceProviderCallbackData callbackData, CancellationToken cancellationToken)
    {
        var httpClient = await _tokenService.GetAuthorizedClient<OnboardingServiceProviderService>(_settings, cancellationToken)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync(callbackUrl, callbackData, cancellationToken)
            .CatchingIntoServiceExceptionFor("trigger-onboarding-provider")
            .ConfigureAwait(false);

        return true;
    }
}