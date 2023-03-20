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

using System.Net.Mime;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using System.Text.RegularExpressions;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Extensions;

public static class ContentTypeMapperExtensions
{
    public static string MapToMediaType(this DocumentMediaTypeId documentMediaType)
    {
        return documentMediaType switch
        {
            DocumentMediaTypeId.JPEG  => MediaTypeNames.Image.Jpeg,
            DocumentMediaTypeId.GIF  => MediaTypeNames.Image.Gif,
            DocumentMediaTypeId.PNG  => "image/png",
            DocumentMediaTypeId.SVG  => "image/svg+xml",
            DocumentMediaTypeId.TIFF  => MediaTypeNames.Image.Tiff,
            DocumentMediaTypeId.PDF  => MediaTypeNames.Application.Pdf,
            DocumentMediaTypeId.JSON  => MediaTypeNames.Application.Json,
            _ => throw new ConflictException($"document mimetype {documentMediaType} is not supported")
        };
    }
    
    public static DocumentMediaTypeId MapToDocumentMediaType(this string mimeType)
    {
        return mimeType.ToLower() switch
        {
            MediaTypeNames.Image.Jpeg => DocumentMediaTypeId.JPEG,
            "image/png" => DocumentMediaTypeId.PNG,
            MediaTypeNames.Image.Gif => DocumentMediaTypeId.GIF,
            "image/svg+xml" => DocumentMediaTypeId.SVG,
            MediaTypeNames.Image.Tiff => DocumentMediaTypeId.TIFF,
            MediaTypeNames.Application.Pdf => DocumentMediaTypeId.PDF,
            MediaTypeNames.Application.Json => DocumentMediaTypeId.JSON,
            _ => throw new UnsupportedMediaTypeException($"mimeType {mimeType} is not supported")
        };
    }
}
