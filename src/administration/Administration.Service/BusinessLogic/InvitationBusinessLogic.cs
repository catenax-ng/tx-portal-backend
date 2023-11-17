/********************************************************************************
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
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic;

public class InvitationBusinessLogic : IInvitationBusinessLogic
{
    private readonly IPortalRepositories _portalRepositories;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="portalRepositories">Portal Repositories</param>
    public InvitationBusinessLogic(IPortalRepositories portalRepositories)
    {
        _portalRepositories = portalRepositories;
    }

    public Task ExecuteInvitation(CompanyInvitationData invitationData)
    {
        if (string.IsNullOrWhiteSpace(invitationData.Email))
        {
            throw new ControllerArgumentException("email must not be empty", "email");
        }
        if (string.IsNullOrWhiteSpace(invitationData.OrganisationName))
        {
            throw new ControllerArgumentException("organisationName must not be empty", "organisationName");
        }
        return ExecuteInvitationInternalAsync(invitationData);
    }

    private async Task ExecuteInvitationInternalAsync(CompanyInvitationData invitationData)
    {
        var (userName, firstName, lastName, email, organisationName) = invitationData;
        var processStepRepository = _portalRepositories.GetInstance<IProcessStepRepository>();
        var processId = processStepRepository.CreateProcess(ProcessTypeId.INVITATION).Id;
        processStepRepository.CreateProcessStep(ProcessStepTypeId.INVITATION_SETUP_IDP, ProcessStepStatusId.TODO, processId);
        _portalRepositories.GetInstance<ICompanyInvitationRepository>().CreateCompanyInvitation(firstName, lastName, email, organisationName, processId, ci =>
            {
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    ci.UserName = userName;
                }
            });
        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }
}
