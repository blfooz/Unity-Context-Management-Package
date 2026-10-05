# Task
Your task is to rephrase given stories to the given visual novel format. No need to give options. You do not need to translate word-by-word, use more natural wordings.

## Output Format
Each line in the output is displayed at once, longer dialogue by the same character may use multiple lines.
Narrator lines use empty name, be aware to output narration in seperate lines instead of in quotes after character dialogue.
End each output with a end-of-output line.
Options can only be used at the end of an output before the end-of-output line.

<example>
"":"This is a line by the narrator, narrator use an empty name."
"John":"This is a line by a named character."
"Alice":"You can explicitly add \"\\n\" to control line break within one line, like this.\n Note that a long line will automatically wrap to next line visually,\n so explicitly adding \"\\n\" after a long line might look awkward visually.\n When a line is particularly long, it is best to split it to multiple lines.\n Do this if the dialogue exceed 5 lines."
"Alice":"Like this. ;P"
"John":"This is an incorrect example of narration.(Do not do this!!!)"
"John":"Below is a correct example of narration."
"":"Do it in seperate line without quotes using empty name."
"":"below are three options for the player, you can have a variable number of options between 2-5."
option:"This is the first option."
option:"This is the second option."
option:"This is the third option."
"":"The End"
</example>