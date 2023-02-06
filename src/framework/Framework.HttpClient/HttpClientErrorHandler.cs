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

using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace Org.Eclipse.TractusX.Portal.Backend.Framework.HttpClient;

public class HttpClientErrorHandler : DelegatingHandler 
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return base.SendAsync(request, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            throw new ServiceException("Service Timed out", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ServiceException("Unauthorized Access", ex, HttpStatusCode.Unauthorized);
        }
        catch (SocketException ex)
        {
            throw new ServiceException("Socket Exception", ex);
        }
        catch (WebSocketException ex)
        {
            throw new ServiceException("Websocket Exception", ex);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode.HasValue && (int) ex.StatusCode > 500 && (int) ex.StatusCode <= 599)
            {
                throw new ServiceException("Websocket Exception", ex, ex.StatusCode.GetValueOrDefault(HttpStatusCode.ServiceUnavailable));
            }

            throw;
        }
    }
}
