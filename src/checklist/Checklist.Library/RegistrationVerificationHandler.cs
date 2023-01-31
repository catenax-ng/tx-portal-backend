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

using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;

public class RegistrationVerificationHandler : IRegistrationVerificationHandler
{
    private readonly IChecklistService _checklistService;

    public RegistrationVerificationHandler(IChecklistService checklistService)
    {
        _checklistService = checklistService;
    }

    public async Task SetRegistrationVerification(Guid applicationId, bool approve, string? comment = null)
    {
        var (processStepId, checklistEntries, processSteps) = await _checklistService.VerifyChecklistEntryAndProcessSteps(applicationId, ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ProcessStepTypeId.VERIFY_REGISTRATION).ConfigureAwait(false);

        var businessPartnerFailed = checklistEntries.Any(entry => entry.EntryTypeId == ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER && entry.EntryStatusId == ApplicationChecklistEntryStatusId.FAILED);
        var createWalletStepExists = processSteps.Any(step => step.ProcessStepTypeId == ProcessStepTypeId.CREATE_IDENTITY_WALLET && step.ProcessStepStatusId == ProcessStepStatusId.TODO);

        _checklistService.FinalizeChecklistEntryAndProcessSteps(
            applicationId,
            ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION,
            entry =>
            {
                entry.ApplicationChecklistEntryStatusId = approve
                    ? ApplicationChecklistEntryStatusId.DONE
                    : ApplicationChecklistEntryStatusId.FAILED;
                entry.Comment = comment;
            },
            processStepId,
            approve && !businessPartnerFailed && !createWalletStepExists
                ? new [] { ProcessStepTypeId.CREATE_IDENTITY_WALLET }
                : null);
    }
}
