using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

using static VisitCommandHandlerSupport;

// HO-EMR-001 §11.4 C1-C3. A uniqueness check is a repository call in the handler (rule 5); every
// other rule lives in the aggregate. Catalogue rows are never deleted -- only deactivated (C-ALL-4).

public sealed class CreateDiagnosisCodeCommandHandler(IAggregateRootRepository<DiagnosisCodeAggregate> repository)
    : ICommandHandlerAsync<CreateDiagnosisCodeCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateDiagnosisCodeCommand command)
    {
        if (await repository.ExistAsync(c => c.Code == command.Code))
            return Violation(BusinessViolationCodes.DiagnosisCodeAlreadyExists, nameof(command.Code),
                $"A diagnosis code '{command.Code}' already exists. Choose another code.", nameof(DiagnosisCodeAggregate));

        var code = new DiagnosisCodeAggregate(command.DiagnosisCodeId);
        try { code.Create(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DiagnosisCodeAggregate)); }

        await repository.SaveAsync(code, EventPublishOption.InMemoryPublishOnly);
        return Success(code.Id);
    }
}

public sealed class UpdateDiagnosisCodeCommandHandler(IAggregateRootRepository<DiagnosisCodeAggregate> repository)
    : ICommandHandlerAsync<UpdateDiagnosisCodeCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateDiagnosisCodeCommand command)
    {
        var code = await repository.GetByIdAsync(command.DiagnosisCodeId);
        if (code is null)
            return Violation(BusinessViolationCodes.DiagnosisCodeNotFound, nameof(command.DiagnosisCodeId), "Diagnosis code not found.", nameof(DiagnosisCodeAggregate));

        try { code.Update(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DiagnosisCodeAggregate)); }

        await repository.UpdateAsync(code, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SetDiagnosisCodeActiveCommandHandler(IAggregateRootRepository<DiagnosisCodeAggregate> repository)
    : ICommandHandlerAsync<SetDiagnosisCodeActiveCommand>
{
    public async Task<CommandResponse> HandleAsync(SetDiagnosisCodeActiveCommand command)
    {
        var code = await repository.GetByIdAsync(command.DiagnosisCodeId);
        if (code is null)
            return Violation(BusinessViolationCodes.DiagnosisCodeNotFound, nameof(command.DiagnosisCodeId), "Diagnosis code not found.", nameof(DiagnosisCodeAggregate));

        try { code.SetActive(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DiagnosisCodeAggregate)); }

        await repository.UpdateAsync(code, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class CreateLabAnalyteCommandHandler(IAggregateRootRepository<LabAnalyteAggregate> repository)
    : ICommandHandlerAsync<CreateLabAnalyteCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateLabAnalyteCommand command)
    {
        if (await repository.ExistAsync(a => a.Code == command.Code))
            return Violation(BusinessViolationCodes.LabAnalyteAlreadyExists, nameof(command.Code),
                $"A lab analyte '{command.Code}' already exists. Choose another code.", nameof(LabAnalyteAggregate));

        var analyte = new LabAnalyteAggregate(command.LabAnalyteId);
        try { analyte.Create(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(LabAnalyteAggregate)); }

        await repository.SaveAsync(analyte, EventPublishOption.InMemoryPublishOnly);
        return Success(analyte.Id);
    }
}

public sealed class UpdateLabAnalyteCommandHandler(IAggregateRootRepository<LabAnalyteAggregate> repository)
    : ICommandHandlerAsync<UpdateLabAnalyteCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateLabAnalyteCommand command)
    {
        var analyte = await repository.GetByIdAsync(command.LabAnalyteId);
        if (analyte is null)
            return Violation(BusinessViolationCodes.LabAnalyteNotFound, nameof(command.LabAnalyteId), "Lab analyte not found.", nameof(LabAnalyteAggregate));

        try { analyte.Update(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(LabAnalyteAggregate)); }

        await repository.UpdateAsync(analyte, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SetLabAnalyteActiveCommandHandler(IAggregateRootRepository<LabAnalyteAggregate> repository)
    : ICommandHandlerAsync<SetLabAnalyteActiveCommand>
{
    public async Task<CommandResponse> HandleAsync(SetLabAnalyteActiveCommand command)
    {
        var analyte = await repository.GetByIdAsync(command.LabAnalyteId);
        if (analyte is null)
            return Violation(BusinessViolationCodes.LabAnalyteNotFound, nameof(command.LabAnalyteId), "Lab analyte not found.", nameof(LabAnalyteAggregate));

        analyte.SetActive(command.ToDomainDto());
        await repository.UpdateAsync(analyte, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class CreateNoteTemplateCommandHandler(
    IAggregateRootRepository<NoteTemplateAggregate> repository,
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository)
    : ICommandHandlerAsync<CreateNoteTemplateCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateNoteTemplateCommand command)
    {
        if (await serviceRepository.GetByIdAsync(command.ClinicServiceId) is null)
            return Violation(BusinessViolationCodes.ServiceNotFound, nameof(command.ClinicServiceId), "Service not found.", nameof(NoteTemplateAggregate));

        // One ACTIVE template per service -- the filtered unique index is the race backstop (§12.1).
        if (await repository.ExistAsync(t => t.ClinicServiceId == command.ClinicServiceId && t.IsActive))
            return Violation(BusinessViolationCodes.NoteTemplateAlreadyExistsForService, nameof(command.ClinicServiceId),
                "This service already has an active note template. Edit it or deactivate it first.", nameof(NoteTemplateAggregate));

        var template = new NoteTemplateAggregate(command.NoteTemplateId);
        try { template.Create(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(NoteTemplateAggregate)); }

        await repository.SaveAsync(template, EventPublishOption.InMemoryPublishOnly);
        return Success(template.Id);
    }
}

public sealed class UpdateNoteTemplateCommandHandler(IAggregateRootRepository<NoteTemplateAggregate> repository)
    : ICommandHandlerAsync<UpdateNoteTemplateCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateNoteTemplateCommand command)
    {
        var template = await repository.GetByIdAsync(command.NoteTemplateId);
        if (template is null)
            return Violation(BusinessViolationCodes.NoteTemplateNotFound, nameof(command.NoteTemplateId), "Note template not found.", nameof(NoteTemplateAggregate));

        try { template.Update(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(NoteTemplateAggregate)); }

        await repository.UpdateAsync(template, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SetNoteTemplateActiveCommandHandler(IAggregateRootRepository<NoteTemplateAggregate> repository)
    : ICommandHandlerAsync<SetNoteTemplateActiveCommand>
{
    public async Task<CommandResponse> HandleAsync(SetNoteTemplateActiveCommand command)
    {
        var template = await repository.GetByIdAsync(command.NoteTemplateId);
        if (template is null)
            return Violation(BusinessViolationCodes.NoteTemplateNotFound, nameof(command.NoteTemplateId), "Note template not found.", nameof(NoteTemplateAggregate));

        if (command.IsActive && !template.IsActive
            && await repository.ExistAsync(t => t.ClinicServiceId == template.ClinicServiceId && t.IsActive && t.Id != template.Id))
            return Violation(BusinessViolationCodes.NoteTemplateAlreadyExistsForService, nameof(command.NoteTemplateId),
                "This service already has an active note template. Deactivate it first.", nameof(NoteTemplateAggregate));

        template.SetActive(command.ToDomainDto());
        await repository.UpdateAsync(template, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
