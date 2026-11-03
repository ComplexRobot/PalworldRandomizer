# Original settings fixture

`FormData.original.json` pins the 137 original public property names, CLR types and desktop startup defaults
at revision `e8d698f055e7a7c4ec22d1b51f0e71bb075f3898`.

Values come from `Window/MainPage.xaml` (`TextBox` content and `IsChecked`, including the WPF unchecked
boolean default) and the three selector assignments in `MainPage`'s initialization callback. The original
`FormData` initializers determine the property/control mapping and numeric types. Hidden and conditional
controls are included; visibility never filters a snapshot. `RandomLevelMax` is **65**, not the validation
fallback of 80. `WeightCustom` has 21 entries: index 0 and indices 11–19 are zero; indices 1–10 and 20 come
from their original text boxes. All 19 mode field names and persisted labels are pinned too.

This is test data, not runtime schema metadata. No original application binary is stored as a fixture.
The regression runner embeds this file and checks the actual compiled models, not copies of the DTO classes.
