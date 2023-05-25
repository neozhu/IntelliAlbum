// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanArchitecture.Blazor.Application.Features.Samples.Commands.AddEdit;

public class AddEditSampleCommandValidator : AbstractValidator<AddEditSampleCommand>
{
    public AddEditSampleCommandValidator()
    {

        RuleFor(v => v.Name)
              .MaximumLength(256)
              .NotEmpty();
        RuleFor(v => v.SampleImages).NotEmpty();

    }
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var result = await ValidateAsync(ValidationContext<AddEditSampleCommand>.CreateWithOptions((AddEditSampleCommand)model, x => x.IncludeProperties(propertyName)));
        if (result.IsValid)
            return Array.Empty<string>();
        return result.Errors.Select(e => e.ErrorMessage);
    };
}

