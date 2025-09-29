# User Queries

User queries can be used to perform basic querying on any IQueryable table with a front-facing user text box.

## BASIC OPERATORS: 
- `=` equals 
- `!=` not equals 
- `^` starts with
- `!^` does not start with
- `$` ends with
- `!$` does not end with
- `*` contains
- `!*` does not contain
- `<` less than 
- `>` greater than
- `<=` less than or equal
- `>=` greater than or equal

Use one comma `,` between terms to logically OR them together.

Use one ampersand `&` to logically AND them together.


## LITERAL OPERANDS:

#### Use single or double quotes to declare a literal operand.
- Types are dynamically assigned based on other operands.
- All string operands are case-insensitive.
- Typical escape characters are available. 
`\'` `\"` `\\` `\n` `\t`

#### Number literals don't require quotes.
- Integer and decimal numbers are supported
`1` `3.14` `-1234` `-25`

## NON-LITERAL OPERANDS (class properties):
Any readable property of a class can be marked with a `UserQueryableAttribute` to allow the user to reference that property. Properties unmarked by this attribute cannot be used by User Queries.

A class can be given a `PrimaryUserQueryableAttribute` to specify the default property to compare to.

## ORDERING
Any query can end with `orderby` or `orderbydescending` followed by a property name to order the results.

## TYPICAL QUERIES
```
name * "waffles"
```
-> returns any entity with the text "waffles" inside of its property specified as "name".

```
rating < 10 & type = 'audio/mp3'
```
-> returns any entity with a value less than 10 in its "rating" property AND its type property is exactly "audio/mp3" (case insensitive)

```
length < '01:00:00' orderby length
```
-> returns any entity with a length less than an hour, ordered ascending by its length.

## LITERAL QUERIES
Occurs when the first token (word) in a query is not literal and doesn't refer to any non-literal operands.

The entire query text is interpreted as a literal and is compared with the default property and operator (`*` or `=`) depending on its type.

HOWEVER `orderby` and `orderbydescending` are still functional.

## PROPERTY REFERENCE AND RANGE OPERATIONS
Use the refer operator `:` after a property name to tell all future comparisons to default to that operator.
```
rating: < 2, > 8 
```
-> returns any entity with a rating less than 2 or greater than 8.

Use the range operator `-` in between operands to request a range
```
rating: 1-5
```
-> returns any entity with a rating from 1 through 5.
