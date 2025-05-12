namespace VirtualHealthAPI;

public class FhirBundle<T>
{
    public string ResourceType { get; set; }
    public List<FhirEntry<T>> Entry { get; set; }
}

public class FhirEntry<T>
{
    public T Resource { get; set; }
}

public class Condition
{
    public string Id { get; set; }
    public string ResourceType { get; set; } = "Condition";
    public CodeableConcept Code { get; set; }
    public FhirReference Subject { get; set; }
    public string? ClinicalStatus { get; set; }
    public string VerificationStatus { get; set; }
    public string? RecordedDate { get; set; }
}

public class Observation
{
    public string ResourceType { get; set; } = "Observation";
    public string? Id { get; set; }
    public CodeableConcept Code { get; set; }
    public FhirReference Subject { get; set; }
    public string EffectiveDateTime { get; set; }
    public ValueQuantity? ValueQuantity { get; set; }
    public ValueString? ValueString { get; set; }
    public List<CodeableConcept> Category { get; set; }
}

public class CodeableConcept
{
    public List<Coding> Coding { get; set; }
    public string? Text { get; set; }
}

public class Coding
{
    public string System { get; set; }
    public string Code { get; set; }
    public string Display { get; set; }
}

public class ValueQuantity
{
    public decimal Value { get; set; }
    public string Unit { get; set; }
    public string System { get; set; }
    public string Code { get; set; }
}

public class ValueString
{
    public string Value { get; set; }
}

public class FhirReference
{
    public string Reference { get; set; }
}

