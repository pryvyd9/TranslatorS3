module Hash

open System
open System.Text
open System.Security.Cryptography

let encode (alpha:string, number:int) =

    let b = alpha.Length

    let rec enc (s:string, n:int) =
        match n with
        | x when x <= 0 -> s
        | _ -> enc (s + alpha.[n % b].ToString(), n / b)

    match number with
    | 0 -> alpha.[0].ToString()
    | _ -> enc ("", number)

let md5Int (input:string) =

    let clean (str:string) =
        str.ToLowerInvariant()
           .Trim()

    let computeHash (str:string) =
        let bytes = Encoding.Unicode.GetBytes(str)
        use crypto = new MD5CryptoServiceProvider()
        crypto.ComputeHash(bytes)

    let convert (bytes:byte[]) =
        let i = BitConverter.ToInt32(bytes, 0)
        Math.Abs(i)

    convert (computeHash (clean input))

