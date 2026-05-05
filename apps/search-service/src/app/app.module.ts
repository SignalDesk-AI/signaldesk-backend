import { Module } from '@nestjs/common';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { ConfigModule } from './config/config.module';
import { HealthModule } from './health/health.module';
import { LoggingModule } from './logging/logging.module';
import { MessagingModule } from './messaging/messaging.module';
import { IndexingModule } from './indexing/indexing.module';
import { SearchModule } from './search/search.module';

@Module({
  imports: [
    ConfigModule,
    HealthModule,
    LoggingModule,
    MessagingModule,
    IndexingModule,
    SearchModule,
  ],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
