namespace JobDesk.Services.Databricks;

/// <summary>
/// Server-side authentication modes for the Databricks SQL Statements API.
/// Only <see cref="PatBearer"/> is implemented; do not assume OAuth M2M on Free Edition.
/// </summary>
public enum DatabricksAuthMode
{
    /// <summary>Do not call Databricks (misconfiguration).</summary>
    None = 0,

    /// <summary>Authorization: Bearer &lt;token&gt; from environment (PAT when workspace allows it).</summary>
    PatBearer = 1
}
