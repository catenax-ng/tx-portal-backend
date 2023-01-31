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

    public BpdmProcessHandler(IBpdmBusinessLogic bpdmBusinessLogic, IChecklistService checklistService)
    {
        _bpdmBusinessLogic = bpdmBusinessLogic;
        _checklistService = checklistService;
    }

    public async Task TriggerBpnDataPush(Guid applicationId, string iamUserId, CancellationToken cancellationToken)
    {
        var result = await _checklistService
            .VerifyChecklistEntryAndProcessSteps(
                applicationId,
                ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER,
                ApplicationChecklistEntryStatusId.TO_DO,
                ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH)
            .ConfigureAwait(false);

        await _bpdmBusinessLogic.TriggerBpnDataPush(applicationId, iamUserId, cancellationToken).ConfigureAwait(false);
        _checklistService.FinalizeChecklistEntryAndProcessSteps(
            applicationId,
            ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER,
            entry => entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.IN_PROGRESS,
            result.ProcessStepId);
    }

    public async Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStep>?,bool)> HandleBpnPull(Guid applicationId, ImmutableDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist, IEnumerable<ProcessStep> processSteps, CancellationToken cancellationToken)
    {
        var businessPartnerNumber = string.Empty; // TODO add bpdm get legal entity call returning businessPartnerNumber
        if (string.IsNullOrWhiteSpace(businessPartnerNumber))
        {
            return (null, null, false);
        }
        var registrationValidationFailed = checklist[ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION] == ApplicationChecklistEntryStatusId.FAILED;
        var createWalletStepExists = processSteps.Any(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_IDENTITY_WALLET && step.ProcessStepStatusId == ProcessStepStatusId.TODO);

        return (
            entry => entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE,
            !registrationValidationFailed && !createWalletStepExists
                ? _checklistService.ScheduleProcessSteps(applicationId, processSteps, ProcessStepTypeId.CREATE_IDENTITY_WALLET)
                : null,
            true);
    }
}
