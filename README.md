Megadeth
========

Megadeth is a modern solution for modern problems.

A new generation of spam bots in Telegram has started to leave reactions on older messages to get your attention and make you to read their bio (where the spam resides).

It's often impossible to ban or restrict these bots, because they don't even enter the public chats (it's possible to leave reactions without entering the chat in Telegram).

Megadeth is an auxiliary bot which you may run on your own account which will help to deal with those.

Requirements
------------

This bot requires [.NET SDK 6][dotnet-sdk] or later.

How to use
----------

1. Make sure you're registered on https://my.telegram.org/
2. Fill in your details into the beginning of the `Program.cs` file: `ApiId`, `ApiHash` (obtained from my.telegram.org), `PhoneNumber`
3. Start the bot via `dotnet run`, authenticate if required (usually only required once; the bot will reuse the authentication data on future starts)
4. Locate the message which has received a reaction from a spam bot
5. Reply to this message with text "/megadeth XXX", where `XXX` is a starting part of a bot account first or last name

   (this is required to avoid banning normal user accounts in case there are several normal reactions among one from a spam bot on the infected message)
6. Megadeth will ban the bot account in the chat where you've replied the message

[dotnet-sdk]: https://dot.net
