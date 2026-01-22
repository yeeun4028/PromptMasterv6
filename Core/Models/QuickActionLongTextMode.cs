namespace PromptMasterv5.Core.Models
{
    /// <summary>
    /// Defines how long text responses in QuickAction should be handled
    /// </summary>
    public enum QuickActionLongTextMode
    {
        /// <summary>
        /// Save to markdown file and open in external editor
        /// </summary>
        ExternalEditor,

        /// <summary>
        /// Expand window to large mode (2/3 screen) internally
        /// </summary>
        InternalExpand
    }
}
