import 'package:clip_sync_ai/main.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('redactSecrets', () {
    test('masks a GitHub token', () {
      final r = redactSecrets(
        'deploy with ghp_abcdefghijklmnopqrstuvwxyz0123456789AB please',
      );
      expect(r.count, 1);
      expect(r.text, contains('[redacted:github-token]'));
      expect(r.text, isNot(contains('ghp_')));
    });

    test('masks OpenAI and Anthropic keys', () {
      final r = redactSecrets(
        'a sk-proj-abcdefghij1234567890xyz and sk-ant-abcdefghij1234567890',
      );
      expect(r.count, 2);
      expect(r.text, contains('[redacted:openai-key]'));
      expect(r.text, contains('[redacted:anthropic-key]'));
    });

    test('masks an AWS access key', () {
      final r = redactSecrets('key AKIAIOSFODNN7EXAMPLE here');
      expect(r.count, 1);
      expect(r.text, contains('[redacted:aws-access-key]'));
    });

    test('masks a Google key, a Slack token and a Stripe secret', () {
      final r = redactSecrets(
        'g AIzaSyDabcdefghijklmnopqrstuvw01234567890 and '
        'x xoxb-123456789012-abcdefghij plus sk_live_abcdefghijklmnop ok',
      );
      expect(r.count, 3);
      expect(r.text, contains('[redacted:google-api-key]'));
      expect(r.text, contains('[redacted:slack-token]'));
      expect(r.text, contains('[redacted:stripe-secret-key]'));
    });

    test('masks a JWT but keeps the sentence punctuation', () {
      const jwt =
          'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dummy-signature-part-here12.';
      final r = redactSecrets('token $jwt');
      expect(r.count, 1);
      expect(r.text, 'token [redacted:jwt].');
    });

    test('masks a PEM private key block whole', () {
      const pem = '-----BEGIN RSA PRIVATE KEY-----\n'
          'MIIEpAIBAAKCAQEA7b\n'
          '-----END RSA PRIVATE KEY-----';
      final r = redactSecrets('cert:\n$pem\ndone');
      expect(r.count, 1);
      expect(r.text, 'cert:\n[redacted:private-key]\ndone');
    });

    test('masks a bearer token', () {
      final r = redactSecrets('Authorization: Bearer abcdef1234567890');
      expect(r.count, 1);
      expect(r.text, contains('[redacted:bearer-token]'));
    });

    test('masks password and api_key assignments, keeping the label', () {
      final r = redactSecrets(
        'db password=hunter2\nenv API_KEY="abc123XYZ"\nclientSecret: s3cr3t!',
      );
      expect(r.count, 3);
      expect(r.text, contains('password=[redacted:password]'));
      expect(r.text, contains('API_KEY="[redacted:api_key]"'));
      expect(r.text, contains('clientSecret=[redacted:clientsecret]'));
      expect(r.text, isNot(contains('hunter2')));
    });

    test('a second run is a no-op', () {
      final once = redactSecrets('key sk-abcdefghij1234567890xyz and pwd=opensesame');
      expect(once.count, greaterThan(0));
      final twice = redactSecrets(once.text);
      expect(twice.count, 0);
      expect(twice.text, once.text);
    });

    test('plain prose passes through untouched', () {
      const prose =
          'My password is safe with me. Take a token of my love. Ring the bell.';
      final r = redactSecrets(prose);
      expect(r.count, 0);
      expect(r.text, prose);
    });

    test('empty text redacts to nothing', () {
      final r = redactSecrets('   ');
      expect(r.count, 0);
      expect(r.text, '   ');
    });
  });
}
