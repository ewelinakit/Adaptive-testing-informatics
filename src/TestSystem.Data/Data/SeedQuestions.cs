using Microsoft.EntityFrameworkCore;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Data.Data;

public static class SeedQuestions
{
    public static void Seed(ApplicationDbContext db)
    {
        if (db.Questions.Any()) return;

        var admin = db.Users.FirstOrDefault(u => u.Role == UserRole.Admin);
        if (admin == null) return;

        // Subject
        var subject = db.Subjects.FirstOrDefault(s => s.Name == "Інформатика");
        if (subject == null)
        {
            subject = new Subject { Id = Guid.NewGuid(), Name = "Інформатика", Description = "Програмування та алгоритми" };
            db.Subjects.Add(subject);
            db.SaveChanges();
        }

        var adminId = admin.Id;
        var subjectId = subject.Id;
        int orderIndex = 0;

        // ===================== 8 КЛАС =====================
        var t8_1 = AddTopic(db, subjectId, adminId, "8 клас: Алгоритми та їх властивості", orderIndex++);
        var t8_2 = AddTopic(db, subjectId, adminId, "8 клас: Лінійні алгоритми", orderIndex++);
        var t8_3 = AddTopic(db, subjectId, adminId, "8 клас: Розгалуження", orderIndex++);
        var t8_4 = AddTopic(db, subjectId, adminId, "8 клас: Цикли", orderIndex++);

        // Тема 1: Алгоритми та їх властивості
        AddQ(db, t8_1, DifficultyLevel.Easy, "Що таке алгоритм?", null,
            ("Програма", false), ("Послідовність дій для розв'язання задачі", true), ("Файл", false), ("Пристрій", false));
        AddQ(db, t8_1, DifficultyLevel.Easy, "Яка властивість алгоритму означає, що він має закінчуватися?", null,
            ("Масовість", false), ("Дискретність", false), ("Результативність", true), ("Визначеність", false));
        AddQ(db, t8_1, DifficultyLevel.Medium, "Яка властивість означає, що команди алгоритму мають бути чіткими та однозначними?", null,
            ("Дискретність", false), ("Визначеність", true), ("Результативність", false), ("Формальність", false));
        AddQ(db, t8_1, DifficultyLevel.Medium, "Який спосіб подання алгоритму є графічним?", null,
            ("Текстовий", false), ("Блок-схема", true), ("Табличний", false), ("Формульний", false));
        AddQ(db, t8_1, DifficultyLevel.Hard, "Яка властивість алгоритму дозволяє застосовувати його до різних вхідних даних?", null,
            ("Масовість", true), ("Дискретність", false), ("Лінійність", false), ("Визначеність", false));

        // Тема 2: Лінійні алгоритми
        AddQ(db, t8_2, DifficultyLevel.Easy, "Лінійний алгоритм — це алгоритм, у якому:", null,
            ("Є розгалуження", false), ("Команди виконуються послідовно", true), ("Є цикл", false), ("Є вкладені оператори", false));
        AddQ(db, t8_2, DifficultyLevel.Easy, "Який оператор використовується для введення даних?", null,
            ("print", false), ("input", true), ("if", false), ("for", false));
        AddQ(db, t8_2, DifficultyLevel.Medium, "Який буде результат виконання:\na = 5\nb = 3\nprint(a + b)", null,
            ("53", false), ("8", true), ("2", false), ("15", false));
        AddQ(db, t8_2, DifficultyLevel.Medium, "Який тип даних використовується для зберігання дробових чисел?", null,
            ("int", false), ("float", true), ("str", false), ("bool", false));
        AddQ(db, t8_2, DifficultyLevel.Hard, "Який результат виконання:\nx = 4\nx = x + 6\nprint(x)", null,
            ("4", false), ("6", false), ("10", true), ("46", false));

        // Тема 3: Розгалуження
        AddQ(db, t8_3, DifficultyLevel.Easy, "Який оператор використовується для розгалуження?", null,
            ("for", false), ("while", false), ("if", true), ("print", false));
        AddQ(db, t8_3, DifficultyLevel.Easy, "Який тип даних повертає умова?", null,
            ("Числовий", false), ("Логічний", true), ("Текстовий", false), ("Масив", false));
        AddQ(db, t8_3, DifficultyLevel.Medium, "Який результат:\nx = 5\nif x > 3:\n  print(\"Так\")", null,
            ("Нічого", false), ("Так", true), ("Помилка", false), ("5", false));
        AddQ(db, t8_3, DifficultyLevel.Medium, "Який оператор використовується для альтернативної гілки?", null,
            ("elif", false), ("else", true), ("for", false), ("end", false));
        AddQ(db, t8_3, DifficultyLevel.Hard, "Якщо x = 2, що виведе програма:\nif x > 5:\n  print(\"A\")\nelse:\n  print(\"B\")", null,
            ("A", false), ("B", true), ("Нічого", false), ("Помилка", false));

        // Тема 4: Цикли
        AddQ(db, t8_4, DifficultyLevel.Easy, "Який оператор використовується для циклу з лічильником?", null,
            ("if", false), ("for", true), ("else", false), ("input", false));
        AddQ(db, t8_4, DifficultyLevel.Easy, "Який цикл виконується, поки умова істинна?", null,
            ("for", false), ("while", true), ("if", false), ("else", false));
        AddQ(db, t8_4, DifficultyLevel.Medium, "Скільки разів виконається цикл:\nfor i in range(3):\n  print(i)", null,
            ("2", false), ("3", true), ("4", false), ("1", false));
        AddQ(db, t8_4, DifficultyLevel.Medium, "Що означає range(5)?", null,
            ("Числа від 1 до 5", false), ("Числа від 0 до 4", true), ("Числа від 0 до 5", false), ("Лише число 5", false));
        AddQ(db, t8_4, DifficultyLevel.Hard, "Який буде результат:\ni = 0\nwhile i < 3:\n  i += 1\nprint(i)", null,
            ("0", false), ("2", false), ("3", true), ("4", false));

        // ===================== 9 КЛАС =====================
        var t9_1 = AddTopic(db, subjectId, adminId, "9 клас: Складені умови та логічні оператори", orderIndex++);
        var t9_2 = AddTopic(db, subjectId, adminId, "9 клас: Вкладені розгалуження", orderIndex++);
        var t9_3 = AddTopic(db, subjectId, adminId, "9 клас: Цикли з параметром та вкладені цикли", orderIndex++);
        var t9_4 = AddTopic(db, subjectId, adminId, "9 клас: Списки (масиви)", orderIndex++);

        // Тема 1: Складені умови та логічні оператори
        AddQ(db, t9_1, DifficultyLevel.Easy, "Який логічний оператор означає \"і\"?", null,
            ("or", false), ("and", true), ("not", false), ("if", false));
        AddQ(db, t9_1, DifficultyLevel.Easy, "Який оператор означає заперечення?", null,
            ("and", false), ("or", false), ("not", true), ("else", false));
        AddQ(db, t9_1, DifficultyLevel.Medium, "Що виведе програма:\nx = 7\nif x > 5 and x < 10:\n  print(\"OK\")", null,
            ("Нічого", false), ("OK", true), ("Помилка", false), ("7", false));
        AddQ(db, t9_1, DifficultyLevel.Medium, "Який результат умови:\n(5 > 3) and (2 > 4)", null,
            ("True", false), ("False", true), ("5", false), ("Помилка", false));
        AddQ(db, t9_1, DifficultyLevel.Hard, "Що виведе програма:\nx = 4\nif x > 5 or x == 4:\n  print(\"YES\")\nelse:\n  print(\"NO\")", null,
            ("YES", true), ("NO", false), ("Нічого", false), ("Помилка", false));

        // Тема 2: Вкладені розгалуження
        AddQ(db, t9_2, DifficultyLevel.Easy, "Вкладене розгалуження — це:", null,
            ("Кілька циклів", false), ("Умова всередині іншої умови", true), ("Помилка коду", false), ("Введення даних", false));
        AddQ(db, t9_2, DifficultyLevel.Easy, "Який оператор дозволяє перевірити декілька умов послідовно?", null,
            ("elif", true), ("while", false), ("for", false), ("input", false));
        AddQ(db, t9_2, DifficultyLevel.Medium, "Що виведе код:\nx = 10\nif x > 5:\n  if x < 15:\n    print(\"A\")", null,
            ("A", true), ("Нічого", false), ("10", false), ("Помилка", false));
        AddQ(db, t9_2, DifficultyLevel.Medium, "Якщо x = 3, що виведе:\nif x > 5:\n  print(\"A\")\nelse:\n  print(\"B\")", null,
            ("A", false), ("B", true), ("Нічого", false), ("Помилка", false));
        AddQ(db, t9_2, DifficultyLevel.Hard, "Якщо x = 6:\nif x > 5:\n  if x > 10:\n    print(\"A\")\n  else:\n    print(\"B\")", null,
            ("A", false), ("B", true), ("Нічого", false), ("Помилка", false));

        // Тема 3: Цикли з параметром та вкладені цикли
        AddQ(db, t9_3, DifficultyLevel.Easy, "Скільки разів виконається:\nfor i in range(5):\n  print(i)", null,
            ("4", false), ("5", true), ("6", false), ("1", false));
        AddQ(db, t9_3, DifficultyLevel.Easy, "Яке значення буде останнім у цьому циклі?\nfor i in range(3):\n  pass", null,
            ("1", false), ("2", true), ("3", false), ("0", false));
        AddQ(db, t9_3, DifficultyLevel.Medium, "Що виведе:\nfor i in range(2):\n  for j in range(2):\n    print(\"*\")", null,
            ("2 зірочки", false), ("4 зірочки", true), ("1 зірочка", false), ("3 зірочки", false));
        AddQ(db, t9_3, DifficultyLevel.Medium, "Який результат:\nsum = 0\nfor i in range(1,4):\n  sum += i\nprint(sum)", null,
            ("3", false), ("6", true), ("4", false), ("10", false));
        AddQ(db, t9_3, DifficultyLevel.Hard, "Скільки разів виконається внутрішній цикл:\nfor i in range(3):\n  for j in range(2):\n    print(j)", null,
            ("3", false), ("6", true), ("5", false), ("2", false));

        // Тема 4: Списки (масиви)
        AddQ(db, t9_4, DifficultyLevel.Easy, "Як створити список?", null,
            ("a = (1,2,3)", false), ("a = [1,2,3]", true), ("a = {1,2,3}", false), ("a = <1,2,3>", false));
        AddQ(db, t9_4, DifficultyLevel.Easy, "Як отримати перший елемент списку a?", null,
            ("a(0)", false), ("a[0]", true), ("a[1]", false), ("first(a)", false));
        AddQ(db, t9_4, DifficultyLevel.Medium, "Що виведе:\na = [1,2,3]\nprint(len(a))", null,
            ("2", false), ("3", true), ("1", false), ("Помилка", false));
        AddQ(db, t9_4, DifficultyLevel.Medium, "Що виведе:\na = [5,10,15]\nprint(a[1])", null,
            ("5", false), ("10", true), ("15", false), ("1", false));
        AddQ(db, t9_4, DifficultyLevel.Hard, "Що виведе:\na = [1,2,3]\na.append(4)\nprint(a)", null,
            ("[1,2,3]", false), ("[1,2,3,4]", true), ("4", false), ("Помилка", false));

        // ===================== 10 КЛАС =====================
        var t10_1 = AddTopic(db, subjectId, adminId, "10 клас: Функції", orderIndex++);
        var t10_2 = AddTopic(db, subjectId, adminId, "10 клас: Рядки (string)", orderIndex++);
        var t10_3 = AddTopic(db, subjectId, adminId, "10 клас: Словники (dict)", orderIndex++);
        var t10_4 = AddTopic(db, subjectId, adminId, "10 клас: Алгоритмічні задачі", orderIndex++);

        // Тема 1: Функції
        AddQ(db, t10_1, DifficultyLevel.Easy, "Для чого використовуються функції?", null,
            ("Для зберігання даних", false), ("Для повторного використання коду", true), ("Для створення списків", false), ("Для циклів", false));
        AddQ(db, t10_1, DifficultyLevel.Easy, "Як оголошується функція в Python?", null,
            ("function myFunc()", false), ("def myFunc():", true), ("func myFunc()", false), ("create myFunc()", false));
        AddQ(db, t10_1, DifficultyLevel.Medium, "Що виведе код:\ndef add(a, b):\n  return a + b\nprint(add(3, 4))", null,
            ("34", false), ("7", true), ("12", false), ("Помилка", false));
        AddQ(db, t10_1, DifficultyLevel.Medium, "Що означає оператор return?", null,
            ("Виводить значення", false), ("Завершує програму", false), ("Повертає значення з функції", true), ("Створює цикл", false));
        AddQ(db, t10_1, DifficultyLevel.Hard, "Що виведе код:\ndef test(x):\n  return x * 2\na = test(5)\nprint(a)", null,
            ("5", false), ("10", true), ("25", false), ("Помилка", false));

        // Тема 2: Рядки (string)
        AddQ(db, t10_2, DifficultyLevel.Easy, "Який тип даних використовується для тексту?", null,
            ("int", false), ("float", false), ("str", true), ("bool", false));
        AddQ(db, t10_2, DifficultyLevel.Easy, "Як отримати довжину рядка s?", null,
            ("size(s)", false), ("length(s)", false), ("len(s)", true), ("count(s)", false));
        AddQ(db, t10_2, DifficultyLevel.Medium, "Що виведе:\ns = \"Hello\"\nprint(s[1])", null,
            ("H", false), ("e", true), ("l", false), ("o", false));
        AddQ(db, t10_2, DifficultyLevel.Medium, "Що виведе:\ns = \"Hi\"\nprint(s * 3)", null,
            ("Hi3", false), ("HiHiHi", true), ("3Hi", false), ("Помилка", false));
        AddQ(db, t10_2, DifficultyLevel.Hard, "Що виведе:\ns = \"Python\"\nprint(s[-1])", null,
            ("P", false), ("n", true), ("o", false), ("Помилка", false));

        // Тема 3: Словники (dict)
        AddQ(db, t10_3, DifficultyLevel.Easy, "Як створити словник?", null,
            ("a = [ ]", false), ("a = { }", true), ("a = ( )", false), ("a = < >", false));
        AddQ(db, t10_3, DifficultyLevel.Easy, "Як отримати значення за ключем key у словнику d?", null,
            ("d.key", false), ("d(key)", false), ("d[key]", true), ("key[d]", false));
        AddQ(db, t10_3, DifficultyLevel.Medium, "Що виведе:\nd = {\"a\": 5, \"b\": 10}\nprint(d[\"a\"])", null,
            ("a", false), ("5", true), ("10", false), ("Помилка", false));
        AddQ(db, t10_3, DifficultyLevel.Medium, "Що робить метод keys()?", null,
            ("Повертає значення", false), ("Повертає всі ключі", true), ("Видаляє ключ", false), ("Створює словник", false));
        AddQ(db, t10_3, DifficultyLevel.Hard, "Що виведе:\nd = {\"x\": 1}\nd[\"y\"] = 2\nprint(len(d))", null,
            ("1", false), ("2", true), ("3", false), ("Помилка", false));

        // Тема 4: Алгоритмічні задачі
        AddQ(db, t10_4, DifficultyLevel.Easy, "Що робить функція max([1,5,3])?", null,
            ("Повертає 1", false), ("Повертає 3", false), ("Повертає 5", true), ("Сортує список", false));
        AddQ(db, t10_4, DifficultyLevel.Easy, "Який оператор використовується для перевірки наявності елемента у списку?", null,
            ("in", true), ("find", false), ("has", false), ("exist", false));
        AddQ(db, t10_4, DifficultyLevel.Medium, "Що виведе:\na = [1,2,3,4]\ncount = 0\nfor i in a:\n  if i % 2 == 0:\n    count += 1\nprint(count)", null,
            ("1", false), ("2", true), ("3", false), ("4", false));
        AddQ(db, t10_4, DifficultyLevel.Medium, "Що виведе:\na = [3,1,4]\na.sort()\nprint(a)", null,
            ("[3,1,4]", false), ("[1,3,4]", true), ("[4,3,1]", false), ("Помилка", false));
        AddQ(db, t10_4, DifficultyLevel.Hard, "Що виведе:\na = [1,2,3]\ntotal = 0\nfor i in a:\n  total += i\nprint(total)", null,
            ("3", false), ("6", true), ("5", false), ("1", false));

        // ===================== 11 КЛАС =====================
        var t11_1 = AddTopic(db, subjectId, adminId, "11 клас: Рекурсія", orderIndex++);
        var t11_2 = AddTopic(db, subjectId, adminId, "11 клас: Алгоритми сортування", orderIndex++);
        var t11_3 = AddTopic(db, subjectId, adminId, "11 клас: Основи ООП", orderIndex++);
        var t11_4 = AddTopic(db, subjectId, adminId, "11 клас: Алгоритмічні задачі підвищеної складності", orderIndex++);

        // Тема 1: Рекурсія
        AddQ(db, t11_1, DifficultyLevel.Easy, "Рекурсія — це:", null,
            ("Цикл for", false), ("Виклик функції всередині самої себе", true), ("Умова if", false), ("Список", false));
        AddQ(db, t11_1, DifficultyLevel.Easy, "Для правильної роботи рекурсії обов'язково потрібна:", null,
            ("Змінна", false), ("Базова умова завершення", true), ("Цикл", false), ("Список", false));
        AddQ(db, t11_1, DifficultyLevel.Medium, "Що виведе код:\ndef f(n):\n  if n == 0:\n    return 0\n  return n + f(n-1)\nprint(f(3))", null,
            ("3", false), ("6", true), ("0", false), ("Помилка", false));
        AddQ(db, t11_1, DifficultyLevel.Medium, "Скільки разів викличеться функція f при f(2)?\ndef f(n):\n  if n == 0:\n    return 0\n  return f(n-1)", null,
            ("1", false), ("2", false), ("3", true), ("Безкінечно", false));
        AddQ(db, t11_1, DifficultyLevel.Hard, "Що виведе:\ndef fact(n):\n  if n == 1:\n    return 1\n  return n * fact(n-1)\nprint(fact(4))", null,
            ("10", false), ("24", true), ("16", false), ("4", false));

        // Тема 2: Алгоритми сортування
        AddQ(db, t11_2, DifficultyLevel.Easy, "Який алгоритм сортування порівнює сусідні елементи?", null,
            ("Швидке сортування", false), ("Бульбашкове сортування", true), ("Пошук максимуму", false), ("Лінійний пошук", false));
        AddQ(db, t11_2, DifficultyLevel.Easy, "Що робить метод sort()?", null,
            ("Видаляє елемент", false), ("Сортує список", true), ("Додає елемент", false), ("Реверсує список", false));
        AddQ(db, t11_2, DifficultyLevel.Medium, "Який результат:\na = [4,2,1]\na.sort()\nprint(a)", null,
            ("[4,2,1]", false), ("[1,2,4]", true), ("[4,1,2]", false), ("Помилка", false));
        AddQ(db, t11_2, DifficultyLevel.Medium, "Яка складність бульбашкового сортування у найгіршому випадку?", null,
            ("O(n)", false), ("O(n²)", true), ("O(log n)", false), ("O(1)", false));
        AddQ(db, t11_2, DifficultyLevel.Hard, "Скільки обмінів потрібно мінімально для повністю відсортованого списку в бульбашковому сортуванні?", null,
            ("1", false), ("n", false), ("0", true), ("n²", false));

        // Тема 3: Основи ООП
        AddQ(db, t11_3, DifficultyLevel.Easy, "Клас — це:", null,
            ("Тип змінної", false), ("Шаблон для створення об'єктів", true), ("Функція", false), ("Цикл", false));
        AddQ(db, t11_3, DifficultyLevel.Easy, "Об'єкт — це:", null,
            ("Конкретний екземпляр класу", true), ("Умова", false), ("Список", false), ("Алгоритм", false));
        AddQ(db, t11_3, DifficultyLevel.Medium, "Що означає __init__?", null,
            ("Видалення об'єкта", false), ("Конструктор класу", true), ("Метод сортування", false), ("Цикл", false));
        AddQ(db, t11_3, DifficultyLevel.Medium, "Що виведе:\nclass A:\n  def __init__(self, x):\n    self.x = x\nobj = A(5)\nprint(obj.x)", null,
            ("5", true), ("x", false), ("A", false), ("Помилка", false));
        AddQ(db, t11_3, DifficultyLevel.Hard, "Що таке інкапсуляція?", null,
            ("Повторення коду", false), ("Приховування внутрішніх даних класу", true), ("Рекурсія", false), ("Сортування", false));

        // Тема 4: Алгоритмічні задачі підвищеної складності
        AddQ(db, t11_4, DifficultyLevel.Easy, "Який алгоритм використовується для пошуку елемента в відсортованому списку?", null,
            ("Лінійний пошук", false), ("Бінарний пошук", true), ("Сортування", false), ("Рекурсія", false));
        AddQ(db, t11_4, DifficultyLevel.Easy, "Яка складність бінарного пошуку?", null,
            ("O(n)", false), ("O(n²)", false), ("O(log n)", true), ("O(1)", false));
        AddQ(db, t11_4, DifficultyLevel.Medium, "Що виведе:\na = [1,2,3,4]\nprint(sum(a))", null,
            ("4", false), ("10", true), ("24", false), ("Помилка", false));
        AddQ(db, t11_4, DifficultyLevel.Medium, "Що виведе:\na = [1,2,3]\nprint(a[::-1])", null,
            ("[1,2,3]", false), ("[3,2,1]", true), ("[2,3,1]", false), ("Помилка", false));
        AddQ(db, t11_4, DifficultyLevel.Hard, "Який буде результат:\ndef f(n):\n  if n <= 1:\n    return n\n  return f(n-1) + f(n-2)\nprint(f(5))",
            "Це рекурсивне обчислення числа Фібоначчі",
            ("5", true), ("8", false), ("3", false), ("10", false));

        db.SaveChanges();
    }

    private static Guid AddTopic(ApplicationDbContext db, Guid subjectId, Guid adminId, string title, int order)
    {
        var topic = new TopicModule
        {
            Id = Guid.NewGuid(),
            Title = title,
            SubjectId = subjectId,
            OrderIndex = order,
            CreatedByUserId = adminId
        };
        db.TopicModules.Add(topic);
        db.SaveChanges();
        return topic.Id;
    }

    private static void AddQ(ApplicationDbContext db, Guid topicId, DifficultyLevel difficulty,
        string text, string? explanation,
        (string text, bool correct) a, (string text, bool correct) b,
        (string text, bool correct) c, (string text, bool correct) d)
    {
        int points = difficulty switch
        {
            DifficultyLevel.Easy => 1,
            DifficultyLevel.Medium => 2,
            DifficultyLevel.Hard => 3,
            _ => 1
        };

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Text = text,
            Explanation = explanation,
            TopicModuleId = topicId,
            DifficultyLevel = difficulty,
            Points = points,
            IsActive = true
        };

        var options = new[] { a, b, c, d };
        for (int i = 0; i < options.Length; i++)
        {
            question.AnswerOptions.Add(new AnswerOption
            {
                Id = Guid.NewGuid(),
                Text = options[i].text,
                IsCorrect = options[i].correct,
                OrderIndex = i
            });
        }

        db.Questions.Add(question);
    }
}
