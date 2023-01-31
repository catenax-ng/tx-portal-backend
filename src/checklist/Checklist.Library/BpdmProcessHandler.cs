/********************************************************************************
 * Copyright (c) 2021,2022 Microsoft and BMW Group AG
 * Copyright (c) 2021,2022 Contributors to the Eclipse Foundation
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

using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;

public class BpdmProcessHandler : IBpdmProcessHandler
{
    private readonly IBpdmBusinessLogic _bpdmBusinessLogic;
    private readonly IChecklistService _checklistService;

    BpdmProcessHandler(IBpdmBusinessLogic bpdmBusinessLogic, IChecklistService checklistService)
    {
        _bpdmBusinessLogic = bpdmBusinessLogic;
        _checklistService = checklistService;
    }

    public async Task TriggerBpnDataPush(Guid applicationId, string iamUserId, CancellationToken cancellationToken)
    {
        var followUpStep = new [] { ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL };
        var processStepId = await _checklistService.VerifyChecklistEntryAndProcessSteps(applicationId, ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH, followUpStep).ConfigureAwait(false);
        await _bpdmBusinessLogic.TriggerBpnDataPush(applicationId, iamUserId, cancellationToken).ConfigureAwait(false);
        _checklistService.FinalizeChecklistEntryAndProcessSteps(applicationId, ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.IN_PROGRESS, processStepId, followUpStep);
    }

    public async Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStep>?,bool)> HandleBpnPull(Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        var businessPartnerNumber = string.Empty; // TODO add bpdm get legal entity call returning businessPartnerNumber
        if (string.IsNullOrWhiteSpace(businessPartnerNumber))
        {
            return (null, null, false);
        }

        // TODO implement Handle Registration Verification - CREATE_IDENTITY_WALLET will not be triggered otherwise:
        var nextSteps = checklist[ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION] == ApplicationChecklistEntryStatusId.DONE
            ? _checklistService.ScheduleProcessSteps(applicationId, processSteps, ProcessStepTypeId.CREATE_IDENTITY_WALLET)
            : null;
        return (entry => entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE, nextSteps, true);
    }
}
