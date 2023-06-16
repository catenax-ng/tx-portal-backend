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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models.Validation;

namespace Org.Eclipse.TractusX.Portal.Backend.Framework.Models.Tests;

public class ValidationTests
{
    #region DistinctValuesAttribute

    public class Foo : ConfigurationProvider
    {

    }

    public class TestSettings
    {
        [DistinctValues]
        public IEnumerable<string> StringProperty { get; set; } = null!;

        [DistinctValues("x => x.Key")]
        public IEnumerable<KeyValuePair<string, string>> TypedProperty { get; set; } = null!;

        [DistinctValues("x => x.Foo")]
        public IEnumerable<KeyValuePair<string, string>> InvalidProperty { get; set; } = null!;
    }

    [Fact]
    public void Distinct_ReturnsExpected()
    {
        // Arrange
        var settings = new TestSettings()
        {
            StringProperty = new[] { "foo", "bar", "baz" },
            TypedProperty = new[] {
                new KeyValuePair<string, string>("foo", "value1"),
                new KeyValuePair<string, string>("bar", "value2"),
                new KeyValuePair<string, string>("baz", "value3")
            }
        };

        var sut = new DistinctValuesValidation<TestSettings>("settings");

        // Act
        var result = sut.Validate("settings", settings);

        // Assert
        result.Should().NotBeNull().And.Match<ValidateOptionsResult>(r =>
            !r.Skipped &&
            r.Succeeded &&
            !r.Failed &&
            r.FailureMessage == null
        );
    }

    [Fact]
    public void Missing_ReturnsExpected()
    {
        // Arrange
        var settings = new TestSettings();

        var sut = new DistinctValuesValidation<TestSettings>("settings");

        // Act
        var result = sut.Validate("settings", settings);

        // Assert
        result.Should().NotBeNull().And.Match<ValidateOptionsResult>(r =>
            !r.Skipped &&
            r.Succeeded &&
            !r.Failed &&
            r.FailureMessage == null
        );
    }

    [Fact]
    public void Duplicates_ReturnsExpected()
    {
        // Arrange
        var settings = new TestSettings()
        {
            StringProperty = new[] { "foo", "bar", "foo" },
            TypedProperty = new[] {
                new KeyValuePair<string, string>("foo", "value1"),
                new KeyValuePair<string, string>("bar", "value2"),
                new KeyValuePair<string, string>("foo", "value3")
            }
        };

        var sut = new DistinctValuesValidation<TestSettings>("settings");

        // Act
        var result = sut.Validate("settings", settings);

        // Assert
        result.Should().NotBeNull().And.Match<ValidateOptionsResult>(r =>
            !r.Skipped &&
            !r.Succeeded &&
            r.Failed &&
            r.FailureMessage == "DataAnnotation validation failed for members: 'StringProperty' with the error: 'foo are duplicate values for StringProperty.'.; DataAnnotation validation failed for members: 'TypedProperty' with the error: '[foo, value3] are duplicate values for TypedProperty.'."
        );
    }

    [Fact]
    public void InvalidProperty_ThrowsExpected()
    {
        // Arrange
        var settings = new TestSettings()
        {
            InvalidProperty = new[] {
                new KeyValuePair<string, string>("foo", "value1"),
                new KeyValuePair<string, string>("bar", "value2"),
                new KeyValuePair<string, string>("foo", "value3")
            }
        };

        var sut = new DistinctValuesValidation<TestSettings>("settings");

        var Act = () => sut.Validate("settings", settings);

        // Act
        var result = Assert.Throws<UnexpectedConditionException>(Act);

        // Assert
        result.Should().NotBeNull().And.Match<UnexpectedConditionException>(r =>
            r.Message == "invalid selector x => x.Foo for type System.Collections.Generic.KeyValuePair`2[System.String,System.String]");
    }

    #endregion
}
