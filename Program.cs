using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class DrinkSessionBot
{
    private readonly TelegramBotClient _botClient;
    private readonly Dictionary<long, List<string>> _choices = new Dictionary<long, List<string>>();
    private readonly Dictionary<long, Dictionary<long, string>> _pickedItems = new Dictionary<long, Dictionary<long, string>>();

    public DrinkSessionBot(string botToken)
    {
        _botClient = new TelegramBotClient(botToken);
    }

    public async Task StartReceiving(CancellationToken cancellationToken)
    {
        Console.WriteLine("Bot is starting...");
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Listen to all update types
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );

        var botDetails = await _botClient.GetMe(cancellationToken);
        Console.WriteLine($"Bot @{botDetails.Username} is running...");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message?.Text == null)
            return;

        var message = update.Message;
        var chatId = message.Chat.Id;
        var userId = message.From!.Id; // Null-forgiving operator ensures this won't throw

        if (message.Text.StartsWith("/start"))
        {
            await SendWelcomeMessage(chatId, cancellationToken);
            return;
        }

        if (message.Text.StartsWith("/suggest"))
        {
            var suggestion = message.Text.Substring("/suggest".Length).Trim();
            if (!string.IsNullOrEmpty(suggestion))
            {
                AddChoice(chatId, suggestion);
                await botClient.SendMessage(chatId, $"Suggestion '{suggestion}' added.", cancellationToken: cancellationToken);
            }
            return;
        }

        if (message.Text.StartsWith("/pick"))
        {
            if (!_choices.ContainsKey(chatId) || _choices[chatId].Count == 0)
            {
                await botClient.SendMessage(chatId, "No choices available yet.", cancellationToken: cancellationToken);
                return;
            }

            if (int.TryParse(message.Text.Substring("/pick".Length).Trim(), out int choiceIndex) &&
                choiceIndex > 0 && choiceIndex <= _choices[chatId].Count)
            {
                PickItem(chatId, userId, _choices[chatId][choiceIndex - 1]);
                await botClient.SendMessage(chatId, $"You picked {_choices[chatId][choiceIndex - 1]}.", cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(chatId, "Invalid choice index.", cancellationToken: cancellationToken);
            }
            return;
        }

        if (message.Text == "/showchoices")
        {
            await ShowChoices(chatId, cancellationToken);
            return;
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error: {exception.Message}");
        return Task.CompletedTask;
    }

    private async Task SendWelcomeMessage(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(chatId, "Welcome to the Drink Session Bot!", cancellationToken: cancellationToken);
    }

    private void AddChoice(long chatId, string choice)
    {
        if (!_choices.ContainsKey(chatId))
            _choices[chatId] = new List<string>();
        _choices[chatId].Add(choice);
    }

    private void PickItem(long chatId, long userId, string item)
    {
        if (!_pickedItems.ContainsKey(chatId))
            _pickedItems[chatId] = new Dictionary<long, string>();
        _pickedItems[chatId][userId] = item;
    }

    private async Task ShowChoices(long chatId, CancellationToken cancellationToken)
    {
        if (!_choices.ContainsKey(chatId) || _choices[chatId].Count == 0)
        {
            await _botClient.SendMessage(chatId, "No choices available yet.", cancellationToken: cancellationToken);
            return;
        }

        var message = "Available choices:\n";
        for (int i = 0; i < _choices[chatId].Count; i++)
        {
            message += $"{i + 1}. {_choices[chatId][i]}\n";
        }

        var keyboard = new ReplyKeyboardMarkup(
            _choices[chatId].Select((c, i) => new KeyboardButton($"/pick {i + 1}")).ToArray()
        )
        {
            ResizeKeyboard = true
        };

        await _botClient.SendMessage(chatId, message, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var botToken = "7617527487:AAH5v0TkqUdexEJXE-jcyZ4dBVBcs0v_dXU";
        var bot = new DrinkSessionBot(botToken);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        await bot.StartReceiving(cts.Token);

        // Block the thread to keep the program running
        Console.WriteLine("Press Ctrl+C to shut down.");
        await Task.Delay(-1, cts.Token); // Keeps the bot alive until cancellation

    }
}
