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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.Services.Service.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Services.Service.ViewModels;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;

namespace Org.Eclipse.TractusX.Portal.Backend.Services.Service.Controllers;

/// <summary>
/// Controller providing actions for displaying, filtering and updating services.
/// </summary>
[Route("api/services/[controller]")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
public class ServiceReleaseController : ControllerBase
{
    private readonly IServiceReleaseBusinessLogic _serviceReleaseBusinessLogic;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="serviceReleaseBusinessLogic">Logic dependency.</param>
    public ServiceReleaseController(IServiceReleaseBusinessLogic serviceReleaseBusinessLogic)
    {
        _serviceReleaseBusinessLogic = serviceReleaseBusinessLogic;
    }

    /// <summary>
    /// Return Agreement Data for offer_type_id Service
    /// </summary>
    /// <remarks>Example: GET: /api/services/servicerelease/agreementData</remarks>
    /// <response code="200">Returns the Cpllection of agreement data</response>
    [HttpGet]
    [Route("agreementData")]
    [Authorize(Roles = "edit_apps")]
    [ProducesResponseType(typeof(IAsyncEnumerable<AgreementDocumentData>), StatusCodes.Status200OK)]
    public IAsyncEnumerable<AgreementDocumentData> GetServiceAgreementDataAsync() =>
        _serviceReleaseBusinessLogic.GetServiceAgreementDataAsync();

    /// <summary>
    /// Retrieves app details for an app referenced by id.
    /// </summary>
    /// <param name="serviceId" example="D3B1ECA2-6148-4008-9E6C-C1C2AEA5C645">ID of the app to retrieve.</param>
    /// <returns>AppDetailsViewModel for requested application.</returns>
    /// <remarks>Example: GET: /api/apps/D3B1ECA2-6148-4008-9E6C-C1C2AEA5C645</remarks>
    /// <response code="200">Returns the requested app details.</response>
    /// <response code="404">App not found.</response>
    [HttpGet]
    [Route("inReview/{serviceId}", Name = nameof(GetServiceDetailsByIdAsync))]
    [Authorize(Roles = "approve_service_release , decline_service_release")]
    [ProducesResponseType(typeof(ServiceData), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public Task<ServiceData> GetServiceDetailsByIdAsync([FromRoute] Guid serviceId) =>
        _serviceReleaseBusinessLogic.GetServiceDetailsByIdAsync(serviceId);
}
