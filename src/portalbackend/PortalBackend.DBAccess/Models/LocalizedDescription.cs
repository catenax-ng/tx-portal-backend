/********************************************************************************
 * Copyright (c) 2022 BMW Group AG
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

using System.ComponentModel.DataAnnotations;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;

/// <summary>
/// Simple model to specify descriptions for a language.
/// </summary>
/// <param name="LanguageCode">Two character language code.</param>
/// <param name="LongDescription">Long description in specified language.</param>
/// <param name="ShortDescription">Short description in specified language.</param>
public record LocalizedDescription(
    [StringLength(2, MinimumLength = 2)] string LanguageCode,
    [MaxLength(4096)] string LongDescription,
    [MaxLength(255)] string ShortDescription);
