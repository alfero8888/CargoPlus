namespace CargoPlus;

internal static class UiText
{
    private static bool Tr => AppLanguage.Turkish;

    public static string Settings => Tr ? "Ayarlar" : "Настройки";
    public static string Exit => Tr ? "Çıkış" : "Выход";
    public static string Menu => Tr ? "Menü" : "Меню";
    public static string CheckCode => Tr ? "Kodu kontrol et" : "Проверить код";
    public static string ReprintReceipt => Tr ? "Mağaza fişini yeniden yazdır" : "Чек магазина ещё раз";
    public static string Finish => Tr ? "Bitir ve iki fiş yazdır" : "Завершить и напечатать два чека";
    public static string AcceptCargo => Tr ? "Yük kabul" : "Принять груз";
    public static string Today => Tr ? "Bugün kabul edilenler" : "Сегодня принято";
    public static string TodayReport => Tr ? "Bugünkü rapor" : "Сегодняшний отчёт";
    public static string TodayEmpty => Tr ? "Bugün henüz çuval kabul edilmedi." : "Сегодня ещё ничего не принято.";
    public static string TodayBags(int count) => Tr ? $"Çuval: {count}" : $"Мешков: {count}";
    public static string TodayKilo(string kilo) => Tr ? $"Kilogram: {kilo}" : $"Килограммы: {kilo}";
    public static string TodayInsured(int count, int dollars) => Tr
        ? $"Sigorta alındı: {count} çuval, {dollars} $"
        : $"Страховка принята: {count} меш., {dollars} $";
    public static string TodayUninsured(int count) => Tr
        ? $"Sigortasız: {count} çuval"
        : $"Без страховки: {count} меш.";
    public static string TodayLine(string type, int bags, string kilo, int insured, int plain) => Tr
        ? $"{type} — {bags} çuval, {kilo} kg, sigorta {insured}, sigortasız {plain}"
        : $"{type} — {bags} меш., {kilo} кг, страховка {insured}, без страховки {plain}";
    public static string Reports => Tr ? "Raporlar" : "Отчёты";
    public static string ReportFrom => Tr ? "Tarihten" : "Дата с";
    public static string ReportTo => Tr ? "Tarihe" : "Дата по";
    public static string ReportSearch => Tr ? "Bul" : "Найти";
    public static string ReportPick => Tr ? "Listeden bir fiş seçin." : "Выберите чек в списке.";
    public static string ReportTitle(int count) => Tr
        ? $"Fişler: {count}. Takip koduna göre gruplandı."
        : $"Чеки: {count}. Сгруппированы по трекинг-коду.";
    public static string Unsynced => Tr ? "Sunucuya gitmeyenler" : "Не отправлено на сервер";
    public static string CheckUpdates => Tr ? "Güncellemeleri denetle" : "Проверить обновления";
    public static string BackMenu => Tr ? "Menüye dön" : "В меню";
    public static string ClientSection => Tr ? "Müşteri" : "Клиент";
    public static string ClientCode => Tr ? "Müşteri kodu" : "Код клиента";
    public static string StoreSection => Tr ? "Mağaza ve çuval" : "Магазин и мешок";
    public static string StorePhone => Tr ? "Mağaza telefonu" : "Телефон магазина";
    public static string StoreName => Tr ? "Mağaza adı" : "Название магазина";
    public static string Type => Tr ? "Gönderi türü" : "Тип отправки";
    public static string Insurance => Tr ? "Sigorta, $" : "Страховка, $";
    public static string Note => Tr ? "Not" : "Заметка";
    public static string Kilos => Tr ? "Kilogram" : "Килограммы";
    public static string PhonePrefix => Tr ? "Telefon: " : "Телефон: ";
    public static string PhoneEmpty => Tr ? "Telefon: —" : "Телефон: —";
    public static string CodeUnchecked => Tr ? "Kod henüz kontrol edilmedi" : "Код ещё не проверен";
    public static string EnterCode => Tr ? "Müşteri kodunu yazın." : "Введите код клиента.";
    public static string CodeMissing => Tr ? "Bu kod veritabanında yok." : "Такого кода в базе нет.";
    public static string CodeMissingBag => Tr ? "Bu kod veritabanında yok. Çuval kabul edilmez." : "Такого кода в базе нет. Мешок не принимаем.";
    public static string CodeOk => Tr ? "Kod var. Çuval kabul edilebilir." : "Код есть в базе. Можно принимать мешки.";
    public static string CheckingCode => Tr ? "Kod sunucuda kontrol ediliyor..." : "Проверяю код на сервере...";
    public static string ServerRejected => Tr ? "Sunucu kodu doğrulamadı." : "Сервер не подтвердил код.";
    public static string SessionOpen => Tr ? "Kapanmamış kabul. Kod daha önce doğrulandı." : "Незакрытая сдача. Код уже был проверен.";
    public static string NeedCode => Tr ? "Müşteri kodunu yazın." : "Введите код клиента.";
    public static string CodeNumber => Tr ? "Müşteri kodu sayı olmalı." : "Код клиента — число.";
    public static string CloseSessionFirst(uint kod) => Tr
        ? $"Şu an {kod} kodunun kabulü açık. Önce fişleri yazdırın."
        : $"Сейчас идёт сдача кода {kod}. Сначала напечатайте чеки.";
    public static string DbNotReady => Tr
        ? "Veritabanı bağlantısı ayarlanmadı. Bunu ana yönetici yapar."
        : "Подключение к базе ещё не настроено. Это делает главный администратор.";
    public static string CodeNotConfirmed => Tr ? "Kod doğrulanmadı. Kabul başlamadı.\n" : "Код не подтверждён. Приём по нему не начат.\n";
    public static string NeedClientFirst => Tr ? "Önce kodu yazın ve sunucu yanıtını bekleyin." : "Сначала введите код и дождитесь ответа сервера.";
    public static string LabelFailed(int kodId, string message) => Tr
        ? $"Çuval {kodId} etiketi yazdırılamadı. {message}"
        : $"Этикетка мешка {kodId} не напечаталась. {message}";
    public static string PickBag => Tr ? "Tablodan bir çuval seçin." : "Выберите мешок в таблице.";
    public static string NoBags => Tr ? "Bu kabulde henüz çuval yok." : "В этой сдаче ещё нет мешков.";
    public static string ReceiptFailed => Tr
        ? "Fişler yazdırılamadı. Kabul kapanmadı, yeniden deneyebilirsiniz.\n"
        : "Чеки не напечатались. Сдача не закрыта, можно попробовать ещё раз.\n";
    public static string CloseApp => Tr ? "Cargo Plus kapatılsın mı?" : "Закрыть Cargo Plus?";
    public static string CloseAppOpen => Tr
        ? "Kabul kapanmadı, fiş yazdırılmadı. Program kapatılsın mı? Çuvallar durur ve bağlantı gelince sunucuya gider."
        : "Сдача ещё не закрыта, чеки не напечатаны. Закрыть программу? Мешки останутся и уйдут на сервер при связи.";
    public static string LogoutAsk => Tr ? "Giriş ekranına dönülsün mü?" : "Выйти из программы на экран входа?";
    public static string LogoutOpen => Tr
        ? "Kabul kapanmadı. Çıkılsın mı? Çuvallar durur, fiş sonraki girişte yazdırılabilir."
        : "Сдача не закрыта. Выйти? Мешки сохранятся, чек можно напечатать после следующего входа.";
    public static string TitleCode => Tr ? "Kod" : "Код";
    public static string TitleSession => Tr ? "Kabul" : "Сдача";
    public static string TitleDb => Tr ? "Veritabanı" : "База";
    public static string TitleBag => Tr ? "Çuval" : "Мешок";
    public static string TitleStore => Tr ? "Mağaza" : "Магазин";
    public static string TitleInsurance => Tr ? "Sigorta" : "Страховка";
    public static string TitleKilo => Tr ? "Kilogram" : "Килограммы";
    public static string TitleLabel => Tr ? "Etiket" : "Этикетка";
    public static string TitleReceipt => Tr ? "Fiş" : "Чек";
    public static string TitleExit => Tr ? "Çıkış" : "Выход";
    public static string TitleNumbers => Tr ? "Çuval numaraları" : "Номера мешков";
    public static string Bags(int count) => Tr ? $"Çuval: {count}" : $"Мешков: {count}";
    public static string StoreLine(int length) => Tr
        ? $"Mağaza satırı birlikte: {length} / 25"
        : $"Вместе в строке магазина: {length} из 25";
    public static string WindowTitle(string? company)
    {
        var firm = company?.Trim() ?? "";
        return firm.Length == 0 ? AppRelease.Product : firm + " — " + AppRelease.Product;
    }

    public static string Header(string company, string name, string level, int next) => Tr
        ? $"{company}     {name}  ·  {level}  ·  sonraki çuval {next}"
        : $"{company}     {name}  ·  {level}  ·  следующий мешок {next}";
    public static string About => Tr ? "Hakkında" : "About";
    public static string AboutTitle => Tr ? "Hakkında" : "About";
    public static string LevelTitle(string code) => code switch
    {
        AccessLevels.Admin => Tr ? "Ana yönetici" : "Главный администратор",
        AccessLevels.Operator => Tr ? "Operatör" : "Оператор",
        _ => code,
    };
    public static string CategoryTitle(string category) =>
        category == AccessLevels.AdminCategory ? (Tr ? "Yönetim" : "Администрация") : category;
    public static string AboutProgram => Tr ? "Program" : "Программа";
    public static string AboutDeveloper => Tr ? "Geliştirici" : "Разработчик";
    public static string AboutDate => Tr ? "Sürüm tarihi" : "Дата версии";
    public static string AboutVersion => Tr ? "Sürüm" : "Версия";
    public static string NumbersEnded => Tr
        ? "Çuval numaraları bitti. Ana yöneticiyi çağırın: yeni aralık gerekir."
        : "Номера мешков закончились. Позовите главного администратора: нужно задать новый диапазон.";
    public static string NumbersLow(int left) => Tr
        ? $"Çuval numaralarının bitmesine {left} kaldı. Numaraları güncellemek gerekir."
        : $"До конца номеров мешков осталось {left}. Обратите внимание: нужно обновить номера.";
    public static string DbMissingStatus => Tr
        ? "Veritabanı ayarlı değil. Yönetici sunucuyu yazmadan kod kabul edilmez."
        : "База не настроена. Приём кода недоступен, пока администратор не укажет сервер.";
    public static string Offline(int pending, string? error) => Tr
        ? $"Bağlantı yok. Gönderilmeyen {pending}. {error}"
        : $"Нет связи. Не отправлено {pending}. {error}";
    public static string AllSent(string when) => Tr ? $"Bağlı. Hepsi gitti. {when}" : $"На связи. Всё отправлено. {when}";
    public static string SomePending(int pending, string when) => Tr
        ? $"Bağlı. Gönderilmeyen {pending}. {when}"
        : $"На связи. Не отправлено {pending}. {when}";
    public static string Speed(double? value) => value is { } amount
        ? amount >= 1024 ? $"{amount / 1024:0.0} MB/s" : $"{amount:0} KB/s"
        : "—";
    public static string Lag(int? ms) => ms is { } value
        ? Tr ? $"{value} ms" : $"{value} мс"
        : Tr ? "yanıt yok" : "нет ответа";
    public static string SyncOk(int synced, string lag, string speed) => Tr
        ? $"Eşitleme normal. Sunucuda {synced}, gönderilmeyen 0. Gecikme {lag}. Hat {speed}."
        : $"Синхронизация в норме. На сервере {synced}, не отправлено 0. Задержка {lag}. Канал {speed}.";
    public static string SyncDb => Tr ? "Veritabanı ayarlı değil." : "База не настроена.";
    public static string SyncOffline(string? error) => Tr ? $"Bağlantı yok. {error}" : $"Связи нет. {error}";
    public static string SyncPending => Tr ? "Henüz sunucuya gitmemiş çuvallar var." : "Есть мешки, которые ещё не ушли на сервер.";
    public static string SyncLine(int pending, int synced, string lag, string speed, string reason) => Tr
        ? $"Gönderilmeyen {pending}. Sunucuda {synced}. Gecikme {lag}. Hat {speed}. {reason}"
        : $"Не отправлено {pending}. На сервере {synced}. Задержка {lag}. Канал {speed}. {reason}";
    public static string SyncIdle => Tr ? "Eşitleme henüz kontrol edilmedi" : "Синхронизация ещё не проверялась";
    public static string HomeNote => Tr
        ? "Kabul edilenler programda 20 gün durur. 20 günden eski olanlar Backups klasörüne gider ve sunucuda kalır."
        : "Принятое хранится в программе 20 дней. Старше 20 дней уходит в папку Backups и остаётся на сервере.";
    public static string ColBag => Tr ? "Çuval" : "Мешок";
    public static string ColTrack => Tr ? "Takip" : "Трекинг";
    public static string ColKilo => Tr ? "Kg" : "Кг";
    public static string ColType => Tr ? "Tür" : "Тип";
    public static string ColInsurance => Tr ? "Sigorta" : "Страховка";
    public static string ColStore => Tr ? "Mağaza" : "Магазин";
    public static string ColTime => Tr ? "Saat" : "Время";
    public static string ColServer => Tr ? "Sunucu" : "Сервер";
    public static string ColCode => Tr ? "Kod" : "Код";
    public static string ColError => Tr ? "Hata" : "Ошибка";
    public static string OnServer => Tr ? "sunucuda" : "на сервере";
    public static string InQueue => Tr ? "sırada" : "в очереди";
    public static string SyncError => Tr ? "hata" : "ошибка";
    public static string TodayTitle => Tr ? "Bugünkü kabuller" : "Сегодняшние приёмы";
    public static string ReportsTitle => Tr ? "20 günlük kabuller" : "Приёмы за 20 дней";
    public static string VisitTitle(string title, int count) => Tr
        ? $"{title}: {count}. Çuvalları açmak için satıra basın."
        : $"{title}: {count}. Нажмите строку, чтобы открыть мешки.";
    public static string VisitHead(uint kod, int count, string track) => Tr
        ? $"Kod {kod} · {count} çuval · takip {track}"
        : $"Код {kod} · {count} меш. · трекинг {track}";
    public static string UnsyncedTitle(int count) => count == 0
        ? Tr ? "Sunucuya gitmeyen: boş" : "Не отправлено на сервер: пусто"
        : Tr ? $"Sunucuya gitmeyen: {count}" : $"Не отправлено на сервер: {count}";
    public static string LoginSubtitle => Tr ? "Yük kabul" : "Приём груза";
    public static string Enter => Tr ? "Giriş" : "Войти";
    public static string AccessLevel => Tr ? "Erişim düzeyi" : "Уровень доступа";
    public static string Category => Tr ? "Kategori" : "Категория";
    public static string OperatorName => Tr ? "Operatör adı" : "Имя оператора";
    public static string Pin => "PIN";
    public static string FirstPin => Tr
        ? "İlk giriş: Администратор, PIN 0000. Girişten sonra ayarlardan PIN'i değiştirin."
        : "Первый вход: Администратор, PIN 0000. После входа смените PIN в настройках.";
    public static string PrinterNone => Tr ? "— seçilmedi —" : "— не выбран —";
    public static string NetworkMark => Tr ? "Ağ" : "Сеть";
    public static string ScanLooking => Tr ? "Yerel ağda yazıcı aranıyor…" : "Ищу принтеры в локальной сети…";
    public static string ScanNone => Tr ? "Bu ağda yazdırma portu yanıt vermedi. IP'yi elle yazın." : "В этой сети никто не ответил на порт печати. Впишите IP вручную.";
    public static string ScanFound(int count) => Tr
        ? $"Bulunan: {count}. Rol doğruysa Kaydet'e basın."
        : $"Найдено: {count}. Если роль угадана верно, нажмите Сохранить.";
    public static string ScanStopped => Tr ? "Arama durdu." : "Поиск остановлен.";
    public static string PickFoundPrinter => Tr ? "Listeden bulunan yazıcıyı seçin." : "Выберите найденный принтер в списке.";
    public static string EnterIp => Tr ? "IP yazın, örneğin 192.168.1.50." : "Введите IP, например 192.168.1.50.";
    public static string PrinterAssigned(string role, string address) => Tr
        ? $"{role}: {address}. Kaydet'e basın."
        : $"{role}: {address}. Нажмите Сохранить.";
    public static string Port9100 => Tr ? "port 9100" : "порт 9100";
    public static string LabelRole => Tr ? "etiket" : "этикетки";
    public static string ScanProgress(int current, int total, int found) => Tr
        ? $"Kontrol {current} / {total}. Bulunan {found}."
        : $"Проверено {current} из {total}. Найдено {found}.";
    public static string ReadingNames => Tr ? "Yazıcı adları okunuyor…" : "Читаю названия принтеров…";
    public static string PickOperator => Tr ? "Operatör seçin." : "Выберите оператора.";
    public static string BadPin => Tr ? "PIN yanlış." : "Неверный PIN.";
    public static string TitleLogin => Tr ? "Giriş" : "Вход";
    public static string Save => Tr ? "Kaydet" : "Сохранить";
    public static string Close => Tr ? "Kapat" : "Закрыть";
    public static string Saved => Tr ? "Ayarlar kaydedildi." : "Настройки сохранены.";
    public static string TabFirm => Tr ? "Firma" : "Фирма";
    public static string TabDb => Tr ? "Veritabanı" : "База";
    public static string TabPrinters => Tr ? "Yazıcılar" : "Принтеры";
    public static string TabNumbers => Tr ? "Çuval numaraları" : "Номера мешков";
    public static string TabOperators => Tr ? "Operatörler" : "Операторы";
    public static string Company => Tr ? "Firma adı" : "Название фирмы";
    public static string Branch => Tr ? "Şube" : "Отделение";
    public static string BolumToggles => Tr
        ? "Listede görünen gönderi türleri. İşareti kaldırılan tür kabul ekranında çıkmaz."
        : "Какие отделы видны в списке при приёме. Снятая галочка убирает отдел из выпадающего списка.";
    public static string OfficeColumn => Tr ? "Şube" : "Отделение";
    public static string OperatorColumn => Tr ? "Kabul eden" : "Принял";
    public static string Language => Tr ? "Program dili" : "Язык программы";
    public static string LanguageHint => Tr
        ? "Dil programı, etiketi ve fişi değiştirir. Türkçe veya Rusça. Kaydet'e basın. Logo, etiket ve fişte firma adının yanında basılır."
        : "Язык меняет программу, бирку и чек. Türkçe или русский. Нажмите «Сохранить». Логотип печатается на этикетке и на чеке рядом с названием фирмы.";
    public static string ChooseLogo => Tr ? "Logo seç" : "Выбрать логотип";
    public static string ClearLogo => Tr ? "Logoyu kaldır" : "Убрать логотип";
    public static string LogoTitle => Tr ? "Logo" : "Логотип";
    public static string LogoBad => Tr ? "Bu dosya logo olarak açılamadı." : "Этот файл не открывается как логотип.";
    public static string UseSsl => Tr ? "SSL kullan" : "Использовать SSL";
    public static string TestConnection => Tr ? "Bağlantıyı dene" : "Проверить подключение";
    public static string ServerAddress => Tr ? "Sunucu adresi" : "Адрес сервера";
    public static string Port => Tr ? "Port" : "Порт";
    public static string Database => Tr ? "Veritabanı" : "База";
    public static string LoginName => Tr ? "Kullanıcı" : "Логин";
    public static string Password => Tr ? "Parola" : "Пароль";
    public static string Connecting => Tr ? "Bağlanılıyor..." : "Подключаюсь...";
    public static string Connected(string table, string client) => Tr
        ? "Bağlantı var. " + table + " " + client + " Kabulün bu veriyi kullanması için Kaydet'e basın."
        : "Подключение есть. " + table + " " + client + " Нажмите «Сохранить», чтобы приемка пользовалась этими данными.";
    public static string TableCreated(string table) => Tr
        ? $"{table} tablosu yoktu, program oluşturdu. Asıl priem_ob kullanılmıyor."
        : $"Таблицы {table} не было, программа её создала. Боевая priem_ob не используется.";
    public static string TrackAdded(string table) => Tr
        ? $"{table} var. track_code kolonu eklendi. Asıl priem_ob kullanılmıyor."
        : $"Таблица {table} есть. Колонка track_code добавлена. Боевая priem_ob не используется.";
    public static string TableReady(string table) => Tr
        ? $"Kayıt {table} tablosuna gider. Asıl priem_ob dokunulmaz."
        : $"Запись идёт в {table}. Боевая priem_ob не трогается.";
    public static string ClientOk => Tr ? "Müşteri kodu resmi client tablosundan okunur." : "Код клиента читается из официальной таблицы client.";
    public static string ClientMissing => Tr ? "Sunucuda client tablosu yok, müşteri kodu kontrol edilemez." : "Таблицы client на сервере нет, код клиента проверить нельзя.";
    public static string NeedConnection => Tr ? "Önce adres, veritabanı ve kullanıcıyı doldurun." : "Сначала заполните адрес, базу и логин.";
    public static string ConnectionFields => Tr ? "Veritabanı için adres, ad ve kullanıcı gerekir." : "Для базы заполните адрес, имя базы и логин.";
    public static string PortRange => Tr ? "Port 1 ile 65535 arasında bir sayıdır. MySQL için genelde 3306." : "Порт — число от 1 до 65535. Для MySQL обычно 3306.";
    public static string CheckBagNumbers => Tr ? "Başlangıç, bitiş ve sonraki çuval numarasını kontrol edin." : "Проверьте начало, конец и следующий номер мешка.";
    public static string LabelPrinter => Tr ? "Etiket yazıcısı" : "Принтер этикеток";
    public static string ReceiptPrinter => Tr ? "Fiş yazıcısı" : "Чековый принтер";
    public static string FindNetwork => Tr ? "Ağda ara" : "Найти в сети";
    public static string ThisLabels => Tr ? "Bu etiket" : "Это этикетки";
    public static string ThisReceipts => Tr ? "Bu fiş" : "Это чеки";
    public static string IpLabels => Tr ? "IP etiketlere" : "IP в этикетки";
    public static string IpReceipts => Tr ? "IP fişlere" : "IP в чеки";
    public static string ManualIp => Tr ? "IP elle" : "IP вручную";
    public static string PrinterHint => Tr
        ? "Önce Windows listesi. «Ağda ara» Argox 214 Plus ve fiş yazıcılarını IP ile arar. Bulunan adres düğmeyle atanır. Sonra Kaydet."
        : "Сначала список Windows. «Найти в сети» ищет Argox 214 Plus и чековые принтеры по IP. Найденный адрес можно назначить кнопкой. Потом нажмите Сохранить.";
    public static string RangeStart => Tr ? "Başlangıç" : "Начало";
    public static string RangeEnd => Tr ? "Bitiş" : "Конец";
    public static string RangeNext => Tr ? "Sonraki numara" : "Следующий номер";
    public static string RestartRange => Tr ? "Baştan başla" : "Начать сначала";
    public static string LastOnServer => Tr ? "Sunucudaki son numara" : "Последний номер на сервере";
    public static string RangeExplain => Tr
        ? "Aralığı siz yazarsınız: hangi numaradan hangi numaraya. Program bu sayıyı saklar. Yedek ve 20 günlük arşiv numarayı sıfırlamaz."
        : "Диапазон пишете вы: от какого номера до какого. Программа запоминает текущий номер. Бэкап и архив за 20 дней его не обнуляют.";
    public static string AskingServer => Tr ? "Sunucuya soruluyor..." : "Спрашиваю сервер...";
    public static string NoServerNumbers => Tr ? "Bu aralıkta sunucuda henüz numara yok." : "В этом диапазоне на сервере номеров ещё нет.";
    public static string ServerMax(int max) => Tr ? $"Bu aralıkta sunucudaki son numara: {max}." : $"Последний номер на сервере в этом диапазоне: {max}.";
    public static string RangeBad => Tr ? "Aralık sayılarını kontrol edin." : "Проверьте числа диапазона.";
    public static string RangeEmpty => Tr ? "Boş numara kalmadı." : "Свободных номеров нет.";
    public static string RangeFree(int remaining, int next) => Tr
        ? $"Boş numara: {remaining}. Sonraki çuval {next} alır."
        : $"Свободно номеров: {remaining}. Следующий мешок получит {next}.";
    public static string Add => Tr ? "Ekle" : "Добавить";
    public static string ChangePin => Tr ? "PIN değiştir" : "Сменить PIN";
    public static string Delete => Tr ? "Sil" : "Удалить";
    public static string Cancel => Tr ? "Vazgeç" : "Отмена";
    public static string NewOperator => Tr ? "Yeni operatör" : "Новый оператор";
    public static string OperatorNeed => Tr ? "Ad, kategori ve dört haneli PIN gerekir." : "Нужны имя, категория и PIN из четырёх цифр.";
    public static string TitleOperator => Tr ? "Operatör" : "Оператор";
    public static string DeleteOperator(string name) => Tr ? $"{name} silinsin mi?" : $"Удалить оператора {name}?";
    public static string NewPin => Tr ? "Yeni PIN" : "Новый PIN";
    public static string RepeatPin => Tr ? "PIN tekrar" : "Повтор PIN";
    public static string PinMismatch => Tr ? "İki alan da aynı dört haneli PIN olmalı." : "Оба поля — один и тот же PIN из четырёх цифр.";
    public static string NeedOperatorName => Tr ? "Operatör adını yazın." : "Введите имя оператора.";
    public static string NeedCategory => Tr ? "Kategori yazın." : "Введите категорию.";
    public static string BadLevel => Tr ? "Bilinmeyen erişim düzeyi." : "Неизвестный уровень доступа.";
    public static string PinFour => Tr ? "PIN dört hanedir." : "PIN — четыре цифры.";
    public static string NameExists => Tr ? "Bu ad zaten var." : "Такое имя уже есть.";
    public static string OperatorMissing => Tr ? "Operatör bulunamadı." : "Оператор не найден.";
    public static string CannotDeleteSelf => Tr ? "Şu an giriş yapan operatör silinemez." : "Нельзя удалить оператора, который сейчас вошёл.";
    public static string NeedOneAdmin => Tr ? "En az bir ana yönetici kalmalı." : "Должен остаться хотя бы один главный администратор.";
    public static string NeedStorePhone => Tr ? "Mağaza telefonunu yazın." : "Введите телефон магазина.";
    public static string NeedStoreName => Tr ? "Mağaza adını yazın." : "Введите название магазина.";
    public static string StoreTooLong(int length) => Tr
        ? $"Telefon ve ad birlikte {length} karakter. Tabloda yer 25."
        : $"Телефон и название вместе занимают {length} символов. В таблице места 25.";
    public static string NeedType => Tr ? "Tür seçin: avia, tır, emanet, paket veya ihracat." : "Выберите тип: авиа, тир, эманет, пакет или играджат.";
    public static string InsuranceNumber => Tr ? "Sigorta tam sayıdır. Varsayılan 10, başka sayı veya 0 yazılabilir." : "Страховка — целое число. По умолчанию 10, можно поставить другое или 0.";
    public static string NoteTooLong => Tr ? "Not 100 karakterden uzun." : "Заметка длиннее 100 символов.";
    public static string KiloFormat => Tr ? "Kilogramı sayı yazın, küsurat virgülle. Örneğin 12,5." : "Килограммы пишите числом, дробь через запятую. Например 12,5.";
    public static string KiloLong => Tr ? "Ağırlık 11 karakterden uzun." : "Вес длиннее 11 символов.";
    public static string KiloZero => Tr ? "Ağırlık sıfırdan büyük olmalı." : "Вес должен быть больше нуля.";
    public static string RangeNegative => Tr ? "Çuval numaraları eksi olamaz." : "Номера мешков не могут быть отрицательными.";
    public static string RangeEndSmall => Tr ? "Aralık sonu başlangıçtan küçük." : "Конец диапазона меньше начала.";
    public static string RangeNextSmall => Tr ? "Sonraki numara başlangıçtan küçük." : "Следующий номер меньше начала диапазона.";
    public static string RangeNextFar => Tr ? "Sonraki numara aralık sonundan ileri." : "Следующий номер дальше конца диапазона.";
    public static string NoLabelPrinter => Tr ? "Ayarlarda etiket yazıcısı seçilmedi." : "В настройках не выбран принтер этикеток.";
    public static string NoReceiptPrinter => Tr ? "Ayarlarda fiş yazıcısı seçilmedi." : "В настройках не выбран чековый принтер.";
    public static string PrinterMissing(string name) => Tr ? $"«{name}» yazıcısı Windows'ta yok." : $"Принтер «{name}» не найден в Windows.";
    public static string NoLabelPage => Tr ? "Yazıcı etiket sayfası vermedi." : "Принтер не дал лист для этикетки.";
    public static string NoReceiptPage => Tr ? "Yazıcı fiş sayfası vermedi." : "Принтер не дал лист для чека.";
    public static string PrinterSilent(string kind, string address) => Tr ? $"{kind} yazıcısı {address} yanıt vermiyor." : $"Принтер {kind} {address} не отвечает.";
    public static string KindLabels => Tr ? "etiket" : "этикеток";
    public static string KindReceipts => Tr ? "fiş" : "чеков";
    public static string NoServer => Tr ? "Sunucuyla bağlantı yok." : "Нет связи с сервером.";
    public static string BadLogin => Tr ? "Sunucu kullanıcıyı veya parolayı reddetti." : "Сервер отклонил логин или пароль.";
    public static string BadDatabase => Tr ? "Sunucuda böyle bir veritabanı yok." : "Такой базы на сервере нет.";
    public static string BadTable => Tr ? "Sunucuda gereken tablo yok." : "На сервере нет нужной таблицы.";
    public static string Timeout => Tr ? "Sunucu zamanında yanıt vermedi." : "Сервер не ответил вовремя.";
    public static string UpdateOffer(Version remote, Version current) => Tr
        ? $"GitHub'da {remote} var. Sizde {current}. İndirip program değiştirilsin mi?"
        : $"На GitHub версия {remote}. У вас {current}. Скачать и заменить программу?";
    public static string UpdateTitle => Tr ? "Güncelleme" : "Обновление";
    public static string UpdateFailed => Tr ? "Güncelleme indirilemedi.\n" : "Не удалось скачать обновление.\n";
    public static string UpdateMissingFile => Tr ? "Çalışan programın dosyası bulunamadı." : "Не найден файл запущенной программы.";
    public static string UpToDate(Version current) => Tr
        ? $"Son sürüm yüklü: {current}."
        : $"Установлена последняя версия {current}.";
    public static string UpdateCheckFailed => Tr ? "GitHub kontrol edilemedi.\n" : "Не удалось проверить GitHub.\n";
    public static string UpdateBusy => Tr ? "Kontrol zaten sürüyor." : "Проверка уже идёт.";
    public static string LocalDbFailed => Tr ? "Yerel veritabanı açılamadı: " : "Не удалось открыть локальную базу: ";
    public static string OpenFailed => Tr ? "Program açılamadı.\n" : "Не удалось открыть программу.\n";
}
