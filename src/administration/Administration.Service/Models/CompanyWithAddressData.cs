/********************************************************************************
 * Copyright (c) 2022 Contributors to the Eclipse Foundation
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
using Org.Eclipse.TractusX.Portal.Backend.Registration.Common;
using System.Text.Json.Serialization;

namespace Org.Eclipse.TractusX.Portal.Backend.Administration.Service.Models;

/// <summary>
/// 
/// </summary>
/// <param name="CompanyId"></param>
/// <param name="Name"></param>
/// <param name="ShortName"></param>
/// <param name="BusinessPartnerNumber"></param>
/// <param name="City"></param>
/// <param name="StreetName"></param>
/// <param name="CountryAlpha2Code"></param>
/// <param name="Region"></param>
/// <param name="StreetAdditional"></param>
/// <param name="StreetNumber"></param>
/// <param name="ZipCode"></param>
/// <param name="AgreementsRoleData"></param>
/// <param name="InvitedUserData"></param>
/// <param name="UniqueIds"></param>
/// <param name="Documents"></param>
/// <param name="Created"></param>
/// <param name="LastChanged"></param>
/// <returns></returns>

public record CompanyWithAddressData(
    Guid CompanyId,
    string Name,
    string ShortName,
    [property: JsonPropertyName("bpn")] string BusinessPartnerNumber,
    string City,
    string StreetName,
    string CountryAlpha2Code,
    string Region,
    string StreetAdditional,
    string StreetNumber,
    string ZipCode,
    [property: JsonPropertyName("companyRoles")] IEnumerable<AgreementsRoleData> AgreementsRoleData,
    [property: JsonPropertyName("companyUser")] IEnumerable<InvitedUserData> InvitedUserData,
    IEnumerable<CompanyUniqueIdData> UniqueIds,
    [property: JsonPropertyName("documents")] IEnumerable<DocumentDetails> Documents,
    DateTimeOffset? Created,
    DateTimeOffset? LastChanged
);

/// <summary>
/// 
/// </summary>
/// <param name="CompanyRole"></param>
/// <param name="Agreements"></param>
/// <returns></returns>
public record AgreementsRoleData(
    CompanyRoleId CompanyRole,
    IEnumerable<AgreementConsentData> Agreements
);

/// <summary>
/// 
/// </summary>
/// <param name="AgreementId"></param>
/// <param name="ConsentStatusId"></param>
/// <returns></returns>
public record AgreementConsentData(
    Guid AgreementId,
    [property: JsonPropertyName("consentStatus")] ConsentStatusId ConsentStatusId
);

/// <summary>
/// 
/// </summary>
/// <param name="UserId"></param>
/// <param name="FirstName"></param>
/// <param name="LastName"></param>
/// <param name="Email"></param>
/// <returns></returns>
public record InvitedUserData(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email
);

/// <summary>
/// 
/// </summary>
/// <param name="DocumentId"></param>
/// <param name="DocumentTypeId"></param>
/// <returns></returns>
public record DocumentDetails(
  [property: JsonPropertyName("documentId")] Guid DocumentId,
  [property: JsonPropertyName("documentType")] DocumentTypeId? DocumentTypeId
);
