﻿using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WhatsAppConvertor.Configuration;
using WhatsAppConvertor.Data;
using WhatsAppConvertor.Exporters;
using WhatsAppConvertor.Models;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<MessageDatabaseOptions>(
            context.Configuration.GetSection(MessageDatabaseOptions.Position));
        services.Configure<WaDatabaseOptions>(
            context.Configuration.GetSection(WaDatabaseOptions.Position));
        services.Configure<ExportOptions>(
            context.Configuration.GetSection(ExportOptions.Position));

        services.AddSingleton<IMessageDataRepository, MessageDataRepository>();
        services.AddSingleton<IContactDataRepository, ContactDataRepository>();
        services.AddAutoMapper(typeof(Program));
        services.AddRazorTemplating();

        services.AddTransient<IExporter, HtmlExporter>();
        services.AddTransient<IExporter, TextExporter>();
        services.AddTransient<IExporter, JsonExporter>();
    })
    .Build();

IMessageDataRepository messageRepo = host.Services.GetRequiredService<IMessageDataRepository>();
IContactDataRepository contactRepo = host.Services.GetRequiredService<IContactDataRepository>();
ExportOptions outputOptions = host.Services.GetRequiredService<IOptions<ExportOptions>>().Value;
IMapper mapper = host.Services.GetRequiredService<IMapper>();
IEnumerable<IExporter> exporters = host.Services.GetServices<IExporter>();

IEnumerable<ChatMessage> chats = await messageRepo.GetChats();
IEnumerable<Contact> contacts = await contactRepo.GetContacts();
IDictionary<string?, Contact> contactsJidDict = contacts.ToDictionary(c => c.RawStringJid);

IList<ChatMessageAndContact> messagesWithContacts = new List<ChatMessageAndContact>();
foreach (ChatMessage chatMessage in chats)
{
    contactsJidDict.TryGetValue(chatMessage.RawStringJid ?? string.Empty, out Contact? contact);
    string? from = chatMessage.MessageFromMe ? "Me" : contact?.DisplayName ?? contact?.RawStringJid;
    string? messageText = chatMessage.FilePath ?? chatMessage.MessageText;

    ChatMessageAndContact message = new()
    {
        Contact = contact,
        ChatMessage = chatMessage
    };

    messagesWithContacts.Add(message);
}

// TODO if the output dir doesnt exist we should try create it?

foreach (IExporter exporter in exporters)
{
    await exporter.ExportAsync(chats, contacts, messagesWithContacts);
}
